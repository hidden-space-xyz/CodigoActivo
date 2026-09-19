using System.Security.Claims;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

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
/// <param name="db">Database context used for persistence.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class TwoFactorTicketValidator(
    CodigoActivoDbContext db,
    IUserRepository users,
    IUnitOfWork uow
)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";
    private const string ChallengeIdClaim = "codigoactivo:challenge";

    /// <summary>
    /// Creates the principal stored in the pending challenge cookie.
    /// </summary>
    /// <param name="userId">Identifier of the user who presented the correct password.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the principal, or <see langword="null"/> when the user cannot log in.</returns>
    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var pending = await FindPendingChallengeAsync(userId, ct);
        return pending is null
            ? null
            : BuildPrincipal(userId, pending.PasswordHash, pending.ChallengeId);
    }

    /// <summary>
    /// Rejects challenge cookies whose user can no longer log in, changed their password, or whose
    /// challenge is no longer the one open on the account.
    /// </summary>
    /// <param name="context">Cookie validation context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
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

        var pending = await FindPendingChallengeAsync(
            userId.Value,
            context.HttpContext.RequestAborted
        );
        if (
            pending is null
            || pending.ChallengeId != presentedChallenge.Value
            || !SessionTicketValidator.FixedTimeEquals(
                presentedFingerprint,
                SessionTicketValidator.Fingerprint(pending.PasswordHash)
            )
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

        var pending = await users.FindAsync(
            candidate => candidate.Id == user && candidate.LoginChallengeId == challenge,
            ct
        );
        if (pending is null)
        {
            return;
        }

        pending.ClearLoginChallenge();
        await uow.SaveChangesAsync(ct);
    }

    private Task<PendingChallenge?> FindPendingChallengeAsync(Guid userId, CancellationToken ct)
    {
        return db
            .Users.AsNoTracking()
            .Where(user =>
                user.Id == userId
                && user.UserStatusTypeId == SeedIds.UserStatusTypes.Active
                && user.PasswordHash != null
                && user.PasswordLockedAt == null
                && user.LoginChallengeId != null
            )
            .Select(user => new PendingChallenge(user.PasswordHash!, user.LoginChallengeId!.Value))
            .SingleOrDefaultAsync(ct);
    }

    private static Guid? ReadChallengeId(ClaimsPrincipal? principal)
    {
        return Guid.TryParse(principal?.FindFirstValue(ChallengeIdClaim), out var id) ? id : null;
    }

    private static ClaimsPrincipal BuildPrincipal(
        Guid userId,
        string passwordHash,
        Guid challengeId
    )
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(PasswordFingerprintClaim, SessionTicketValidator.Fingerprint(passwordHash)),
            new(ChallengeIdClaim, challengeId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TwoFactorAuthentication.Scheme));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
    }

    private sealed record PendingChallenge(string PasswordHash, Guid ChallengeId);
}
