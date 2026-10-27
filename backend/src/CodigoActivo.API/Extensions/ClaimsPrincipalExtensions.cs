using System.Security.Claims;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Reads the account claims that the session cookie carries.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Type of the claim, valued <c>True</c>, that the session cookie of an administrator carries.
    /// </summary>
    public const string IsAdminClaim = "isAdmin";

    /// <summary>
    /// Gets the account identifier from the name identifier claim.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <returns>
    /// The account identifier, or <see langword="null"/> when the claim is missing or not a GUID.
    /// </returns>
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
