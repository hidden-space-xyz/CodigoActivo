using System.Security.Claims;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CodigoActivo.API.Security;

/// <summary>
/// Issues, validates and revokes the session cookie. Every cookie names a stored session and carries
/// the credential stamp of the password it was issued for, so revoking the session or changing the
/// password invalidates it on the next request; the account data behind the claims comes from the
/// session use cases.
/// </summary>
/// <param name="startSession">Handler that opens the stored session.</param>
/// <param name="endSession">Handler that revokes a stored session.</param>
/// <param name="sessionIdentity">Handler that resolves the account behind a live session.</param>
public sealed class SessionTicketValidator(
    ICommandHandler<StartSessionCommand, Result<SessionTicket>> startSession,
    ICommandHandler<EndSessionCommand, Result> endSession,
    IQueryHandler<GetSessionIdentityQuery, SessionIdentity?> sessionIdentity
)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";
    private const string SessionIdClaim = "sid";

    /// <summary>
    /// Opens a session for the account and builds the principal of its cookie.
    /// </summary>
    /// <param name="userId">Identifier of the account that completed the second factor.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the principal, or <see langword="null"/> when the account cannot sign in.</returns>
    public async Task<ClaimsPrincipal?> StartSessionAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var started = await startSession.HandleAsync(
            new StartSessionCommand(UserId.From(userId)),
            ct
        );
        return started.IsFailure
            ? null
            : BuildPrincipal(started.Value.Identity, started.Value.SessionId);
    }

    /// <summary>
    /// Revokes the session named by the cookie, if any.
    /// </summary>
    /// <param name="principal">Principal read from the session cookie.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EndSessionAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        var userId = principal?.GetUserId();
        var sessionId = ReadSessionId(principal);
        if (userId is not { } user || sessionId is not { } session)
        {
            return;
        }

        await endSession.HandleAsync(
            new EndSessionCommand(UserId.From(user), UserSessionId.From(session)),
            ct
        );
    }

    /// <summary>
    /// Validates the session cookie on every request and refreshes its claims when the account data
    /// behind them changed.
    /// </summary>
    /// <param name="context">Cookie validation context of the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = context.Principal?.GetUserId();
        var sessionId = ReadSessionId(context.Principal);
        var presentedFingerprint = context.Principal?.FindFirstValue(PasswordFingerprintClaim);
        if (userId is null || sessionId is null || string.IsNullOrEmpty(presentedFingerprint))
        {
            await RejectAsync(context);
            return;
        }

        var identity = await sessionIdentity.HandleAsync(
            new GetSessionIdentityQuery(
                UserId.From(userId.Value),
                UserSessionId.From(sessionId.Value)
            ),
            context.HttpContext.RequestAborted
        );
        if (
            identity is null
            || !CredentialStamps.Matches(presentedFingerprint, identity.CredentialStamp)
        )
        {
            await RejectAsync(context);
            return;
        }

        var refreshed = BuildPrincipal(identity, sessionId.Value);
        if (!ClaimsMatch(context.Principal!, refreshed))
        {
            context.ReplacePrincipal(refreshed);
            context.ShouldRenew = true;
        }
    }

    private static Guid? ReadSessionId(ClaimsPrincipal? principal)
    {
        return Guid.TryParse(principal?.FindFirstValue(SessionIdClaim), out var id) ? id : null;
    }

    private static ClaimsPrincipal BuildPrincipal(SessionIdentity identity, Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
            new(ClaimTypes.Name, $"{identity.FirstName} {identity.LastName}"),
            new(PasswordFingerprintClaim, identity.CredentialStamp),
            new(SessionIdClaim, sessionId.ToString()),
        };
        if (!string.IsNullOrEmpty(identity.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, identity.Email));
        }

        if (identity.IsAdmin)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString));
            claims.Add(new Claim(ClaimTypes.Role, AllowOnlyAdminAttribute.AdminRole));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
        );
    }

    private static bool ClaimsMatch(ClaimsPrincipal current, ClaimsPrincipal refreshed)
    {
        return current
            .Claims.OrderBy(claim => claim.Type)
            .ThenBy(claim => claim.Value)
            .Select(claim => (claim.Type, claim.Value))
            .SequenceEqual(
                refreshed
                    .Claims.OrderBy(claim => claim.Type)
                    .ThenBy(claim => claim.Value)
                    .Select(claim => (claim.Type, claim.Value))
            );
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
