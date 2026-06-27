using System.Security.Claims;
using CodigoActivo.Domain.Constants;

namespace CodigoActivo.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // Role claims carry the user-type GUID (not the display name), so admin checks
    // stay stable regardless of how the role is named/translated in the database.
    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(SeedIds.UserTypes.Admin.ToString());
}
