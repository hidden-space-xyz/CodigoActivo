using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Orders activity roles read by identifier for attendee lists: in the order of
/// <see cref="ActivityRole"/>, leaders first, and any unknown role last.
/// </summary>
public static class ActivityRoleOrder
{
    /// <summary>
    /// Returns the display position of a role; lower values are listed first.
    /// </summary>
    /// <param name="roleTypeId">Identifier of the role.</param>
    /// <returns>The sort key of the role.</returns>
    public static int Of(Guid roleTypeId)
    {
        return CatalogIds.ActivityRoles.TryGetValue(roleTypeId, out var role)
            ? (int)role
            : int.MaxValue;
    }
}
