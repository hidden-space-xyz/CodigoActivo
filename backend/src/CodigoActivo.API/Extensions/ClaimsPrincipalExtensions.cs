using System.Security.Claims;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Provides reusable extension methods for claims principal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Identifies the is admin claim configuration or policy value.
    /// </summary>
    public const string IsAdminClaim = "isAdmin";

    /// <summary>
    /// Gets the requested user identifier.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <returns>The resulting guid value.</returns>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }

    /// <summary>
    /// Determines whether the authenticated principal has administrator privileges.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(IsAdminClaim, bool.TrueString);
    }
}
