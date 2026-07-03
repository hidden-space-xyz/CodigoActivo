using System.Security.Claims;
using CodigoActivo.Domain.Constants;

namespace CodigoActivo.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(SeedIds.UserTypes.Admin.ToString());
    }
}