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

public sealed class SessionTicketValidator(CodigoActivoDbContext db)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";

    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await FindSessionUserAsync(userId, ct);
        return user is null ? null : BuildPrincipal(user);
    }

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

    private static string Fingerprint(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }

    private static bool FixedTimeEquals(string left, string right)
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
