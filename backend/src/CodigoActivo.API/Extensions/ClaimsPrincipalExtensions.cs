using System.Security.Claims;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Reads the account claims that the session cookie carries.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Role claim value that the session cookie of an editor or an administrator carries.
    /// </summary>
    public const string EditorRole = "editor";

    /// <summary>
    /// Role claim value that the session cookie of an administrator carries.
    /// </summary>
    public const string AdministratorRole = "administrator";

    /// <summary>
    /// Role claim value that the session cookie of the account designated for data protection
    /// carries.
    /// </summary>
    public const string PrivacyOfficerRole = "privacy-officer";

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
    /// Determines whether the authenticated principal holds a staff role or one that includes it.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <param name="staffRole">Staff role required.</param>
    /// <returns>
    /// <see langword="true"/> when it holds the role or one that includes it, or the role is
    /// <see cref="StaffRole.None"/>.
    /// </returns>
    public static bool HasStaffRole(this ClaimsPrincipal principal, StaffRole staffRole)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return staffRole is StaffRole.None || principal.IsInRole(RoleOf(staffRole));
    }

    /// <summary>
    /// Determines whether the authenticated principal is the initial administrator, the
    /// superadministrator.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <returns><see langword="true"/> when it is the initial administrator.</returns>
    public static bool IsSuperAdministrator(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.GetUserId() == InitialAdministrator.Id.Value;
    }

    /// <summary>
    /// Determines whether the authenticated principal is the account designated for data
    /// protection.
    /// </summary>
    /// <param name="principal">Authenticated principal whose claims are inspected.</param>
    /// <returns><see langword="true"/> when it carries the data protection role claim.</returns>
    public static bool IsPrivacyOfficer(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(PrivacyOfficerRole);
    }

    /// <summary>
    /// Gets the role claim values of a staff role: its own and those of the roles it includes.
    /// </summary>
    /// <param name="staffRole">Staff role of the account.</param>
    /// <returns>The role claim values, none for an account without a staff role.</returns>
    public static IReadOnlyList<string> RolesOf(StaffRole staffRole)
    {
        return staffRole switch
        {
            StaffRole.Editor => [EditorRole],
            StaffRole.Administrator => [EditorRole, AdministratorRole],
            _ => [],
        };
    }

    /// <summary>
    /// Gets the role claim value that a staff role requires.
    /// </summary>
    /// <param name="staffRole">Staff role, other than <see cref="StaffRole.None"/>.</param>
    /// <returns>The role claim value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The staff role has no role claim value.
    /// </exception>
    public static string RoleOf(StaffRole staffRole)
    {
        return staffRole switch
        {
            StaffRole.Editor => EditorRole,
            StaffRole.Administrator => AdministratorRole,
            _ => throw new ArgumentOutOfRangeException(nameof(staffRole), staffRole, null),
        };
    }
}
