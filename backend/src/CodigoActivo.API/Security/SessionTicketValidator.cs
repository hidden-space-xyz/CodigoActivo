using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Security;

/// <summary>
/// Validates session ticket input before it is processed.
/// </summary>
/// <param name="db">Database context used for persistence.</param>
public sealed class SessionTicketValidator(CodigoActivoDbContext db)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";

    /// <summary>
    /// Creates a principal.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching claims principal, or <see langword="null"/> when it is not found.</returns>
    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await FindSessionUserAsync(userId, ct);
        return user is null ? null : BuildPrincipal(user);
    }

    /// <summary>
    /// Validates the session ticket state and rejects unsafe configuration.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
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

        var user = await FindSessionUserAsync(userId.Value, context.HttpContext.RequestAborted);
        if (
            user is null
            || !FixedTimeEquals(presentedFingerprint, Fingerprint(user.PasswordHash))
        )
        {
            await RejectAsync(context);
            return;
        }

        var refreshed = BuildPrincipal(user);
        if (!ClaimsMatch(context.Principal!, refreshed))
        {
            context.ReplacePrincipal(refreshed);
            context.ShouldRenew = true;
        }
    }

    private Task<SessionUser?> FindSessionUserAsync(Guid userId, CancellationToken ct)
    {
        return db
            .Users.AsNoTracking()
            .Where(user =>
                user.Id == userId
                && user.UserStatusTypeId == SeedIds.UserStatusTypes.Active
                && user.PasswordHash != null
            )
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

    private static ClaimsPrincipal BuildPrincipal(SessionUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(PasswordFingerprintClaim, Fingerprint(user.PasswordHash)),
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
        return current.Claims.OrderBy(claim => claim.Type).ThenBy(claim => claim.Value)
            .Select(claim => (claim.Type, claim.Value))
            .SequenceEqual(
                refreshed.Claims.OrderBy(claim => claim.Type).ThenBy(claim => claim.Value)
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
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );
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
