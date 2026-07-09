using System.Security.Claims;

namespace CodigoActivo.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public const string IsAdminClaim = "isAdmin";

    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(IsAdminClaim, bool.TrueString);
    }
}
