using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Security;

/// <summary>
/// Opens, validates and revokes the server-side state behind the session cookie. Every ticket names
/// a <c>user_sessions</c> row through its <c>sid</c> claim, so a copied cookie stops working as soon
/// as that row is deleted instead of lasting until the cookie expires.
/// </summary>
/// <param name="db">Database context used for persistence.</param>
/// <param name="sessions">Repository used to persist and retrieve user sessions.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="options">Lifetime shared by the cookie and its session row.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class SessionTicketValidator(
    CodigoActivoDbContext db,
    IUserSessionRepository sessions,
    IUnitOfWork uow,
    IClock clock,
    SessionLifetimeOptions options,
    ILogger<SessionTicketValidator> logger
)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";
    private const string SessionIdClaim = "sid";

    /// <summary>
    /// Opens a session for a user whose second factor was accepted: it records a session row that
    /// expires with the cookie, drops that user's already-expired rows, and returns the principal
    /// carrying the new session identifier.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching claims principal, or <see langword="null"/> when it is not found.</returns>
    public async Task<ClaimsPrincipal?> StartSessionAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await FindSessionUserAsync(userId, sessionId: null, ct);
        if (user is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        await sessions.RemoveAsync(
            candidate => candidate.UserId == userId && candidate.ExpiresAt <= now,
            ct
        );

        var session = new UserSession
        {
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + options.Lifetime,
        };
        await sessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);

        return BuildPrincipal(user, session.Id);
    }

    /// <summary>
    /// Revokes the session named by the presented ticket. Nothing happens when the ticket carries no
    /// usable session identifier or the row was already removed, so callers can sign out regardless.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
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

        var revoked = await sessions.RemoveAsync(
            candidate => candidate.Id == session && candidate.UserId == user,
            ct
        );
        if (revoked > 0)
        {
            logger.SessionEnded(user);
        }
    }

    /// <summary>
    /// Validates the session ticket state and rejects unsafe configuration.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.GetUserId();
        var sessionId = ReadSessionId(context.Principal);
        var presentedFingerprint = context.Principal?.FindFirstValue(PasswordFingerprintClaim);
        if (userId is null || sessionId is null || string.IsNullOrEmpty(presentedFingerprint))
        {
            await RejectAsync(context);
            return;
        }

        var user = await FindSessionUserAsync(
            userId.Value,
            sessionId,
            context.HttpContext.RequestAborted
        );
        if (user is null || !FixedTimeEquals(presentedFingerprint, Fingerprint(user.PasswordHash)))
        {
            await RejectAsync(context);
            return;
        }

        var refreshed = BuildPrincipal(user, sessionId.Value);
        if (!ClaimsMatch(context.Principal!, refreshed))
        {
            context.ReplacePrincipal(refreshed);
            context.ShouldRenew = true;
        }
    }

    private Task<SessionUser?> FindSessionUserAsync(
        Guid userId,
        Guid? sessionId,
        CancellationToken ct
    )
    {
        var candidates = db
            .Users.AsNoTracking()
            .Where(user =>
                user.Id == userId
                && user.UserStatusTypeId == SeedIds.UserStatusTypes.Active
                && user.PasswordHash != null
            );

        if (sessionId is { } session)
        {
            var now = clock.UtcNow;
            candidates = candidates.Where(user =>
                db.UserSessions.Any(row =>
                    row.Id == session && row.UserId == user.Id && row.ExpiresAt > now
                )
            );
        }

        return candidates
            .Select(user => new SessionUser(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PasswordHash!,
                user.IsAdmin
            ))
            .SingleOrDefaultAsync(ct);
    }

    private static Guid? ReadSessionId(ClaimsPrincipal? principal)
    {
        return Guid.TryParse(principal?.FindFirstValue(SessionIdClaim), out var id) ? id : null;
    }

    private static ClaimsPrincipal BuildPrincipal(SessionUser user, Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(PasswordFingerprintClaim, Fingerprint(user.PasswordHash)),
            new(SessionIdClaim, sessionId.ToString()),
        };
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (user.IsAdmin)
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

    internal static string Fingerprint(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }

    internal static bool FixedTimeEquals(string left, string right)
    {
        return left.Length == right.Length
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(left),
                Encoding.ASCII.GetBytes(right)
            );
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private sealed record SessionUser(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string PasswordHash,
        bool IsAdmin
    );
}
