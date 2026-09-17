using System.Security.Claims;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Constants;
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
/// Issues and validates the short-lived tickets of pending second-factor challenges.
/// </summary>
/// <param name="db">Database context used for persistence.</param>
public sealed class TwoFactorTicketValidator(CodigoActivoDbContext db)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";

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
        var passwordHash = await FindPasswordHashAsync(userId, ct);
        return passwordHash is null ? null : BuildPrincipal(userId, passwordHash);
    }

    /// <summary>
    /// Rejects challenge cookies whose user can no longer log in or changed their password.
    /// </summary>
    /// <param name="context">Cookie validation context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.GetUserId();
        var presentedFingerprint = context.Principal?.FindFirstValue(PasswordFingerprintClaim);
        if (userId is null || string.IsNullOrEmpty(presentedFingerprint))
        {
            await RejectAsync(context);
            return;
        }

        var passwordHash = await FindPasswordHashAsync(
            userId.Value,
            context.HttpContext.RequestAborted
        );
        if (
            passwordHash is null
            || !SessionTicketValidator.FixedTimeEquals(
                presentedFingerprint,
                SessionTicketValidator.Fingerprint(passwordHash)
            )
        )
        {
            await RejectAsync(context);
        }
    }

    private Task<string?> FindPasswordHashAsync(Guid userId, CancellationToken ct)
    {
        return db
            .Users.AsNoTracking()
            .Where(user =>
                user.Id == userId
                && user.UserStatusTypeId == SeedIds.UserStatusTypes.Active
                && user.PasswordHash != null
            )
            .Select(user => user.PasswordHash)
            .SingleOrDefaultAsync(ct);
    }

    private static ClaimsPrincipal BuildPrincipal(Guid userId, string passwordHash)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(PasswordFingerprintClaim, SessionTicketValidator.Fingerprint(passwordHash)),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TwoFactorAuthentication.Scheme));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
    }
}
