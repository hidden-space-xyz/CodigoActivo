using System.Security.Claims;
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
/// Names the cookie scheme that remembers a login whose password was accepted but whose second
/// factor is still pending. It grants access to nothing but the second-factor endpoints.
/// </summary>
public static class TwoFactorAuthentication
{
    /// <summary>
    /// Identifies the pending second-factor cookie scheme.
    /// </summary>
    public const string Scheme = "TwoFactor";
}

/// <summary>
/// Issues, validates and closes the short-lived tickets of pending second-factor challenges. A
/// ticket is not self-sufficient: it names the <c>login_challenge_id</c> the password step stored on
/// the account, so a copied challenge cookie stops working as soon as that value is replaced by a
/// newer password step or cleared by an accepted second factor, a lockout, a password change or a
/// sign out, instead of lasting until the cookie expires. An account locked after repeated wrong
/// passwords has no pending challenge either, so a ticket obtained just before the lock is refused.
/// </summary>
/// <param name="pendingChallenge">Handler that resolves the pending challenge of an account.</param>
/// <param name="endChallenge">Handler that closes a pending challenge.</param>
public sealed class TwoFactorTicketValidator(
    IQueryHandler<GetPendingChallengeQuery, PendingChallenge?> pendingChallenge,
    ICommandHandler<EndLoginChallengeCommand, Result> endChallenge
)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";
    private const string ChallengeIdClaim = "codigoactivo:challenge";

    /// <summary>
    /// Builds the ticket of the challenge the password step just opened.
    /// </summary>
    /// <param name="userId">Identifier of the account that passed the password step.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the principal, or <see langword="null"/> when no challenge is pending.</returns>
    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var pending = await pendingChallenge.HandleAsync(new GetPendingChallengeQuery(userId), ct);
        return pending is null
            ? null
            : BuildPrincipal(userId, pending.CredentialStamp, pending.ChallengeId);
    }

    /// <summary>
    /// Validates the pending second-factor cookie on every request.
    /// </summary>
    /// <param name="context">Cookie validation context of the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = context.Principal?.GetUserId();
        var presentedFingerprint = context.Principal?.FindFirstValue(PasswordFingerprintClaim);
        var presentedChallenge = ReadChallengeId(context.Principal);
        if (
            userId is null
            || presentedChallenge is null
            || string.IsNullOrEmpty(presentedFingerprint)
        )
        {
            await RejectAsync(context);
            return;
        }

        var pending = await pendingChallenge.HandleAsync(
            new GetPendingChallengeQuery(userId.Value),
            context.HttpContext.RequestAborted
        );
        if (
            pending is null
            || pending.ChallengeId != presentedChallenge.Value
            || !CredentialStamps.Matches(presentedFingerprint, pending.CredentialStamp)
        )
        {
            await RejectAsync(context);
        }
    }

    /// <summary>
    /// Closes the challenge named by the presented ticket, so a copy of that cookie is refused
    /// immediately. Nothing happens when the ticket carries no challenge or the account already
    /// moved on to a different one.
    /// </summary>
    /// <param name="principal">Principal read from the challenge cookie, if any.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EndChallengeAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        var userId = principal?.GetUserId();
        var challengeId = ReadChallengeId(principal);
        if (userId is not { } user || challengeId is not { } challenge)
        {
            return;
        }

        await endChallenge.HandleAsync(
            new EndLoginChallengeCommand(UserId.From(user), challenge),
            ct
        );
    }

    private static Guid? ReadChallengeId(ClaimsPrincipal? principal)
    {
        return Guid.TryParse(principal?.FindFirstValue(ChallengeIdClaim), out var id) ? id : null;
    }

    private static ClaimsPrincipal BuildPrincipal(
        Guid userId,
        string credentialStamp,
        Guid challengeId
    )
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(PasswordFingerprintClaim, credentialStamp),
            new(ChallengeIdClaim, challengeId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TwoFactorAuthentication.Scheme));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
    }
}
