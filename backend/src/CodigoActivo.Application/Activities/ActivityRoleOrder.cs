using CodigoActivo.Domain.Constants;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Orders activity roles for attendee lists: leaders first, then volunteers, then participants,
/// then any other role.
/// </summary>
public static class ActivityRoleOrder
{
    /// <summary>
    /// Returns the display position of a role; lower values are listed first.
    /// </summary>
    /// <param name="roleTypeId">Identifier of the role type.</param>
    /// <returns>The sort key of the role.</returns>
    public static int Of(Guid roleTypeId)
    {
        return roleTypeId switch
        {
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Leader => 0,
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Volunteer => 1,
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Participant => 2,
            _ => 3,
        };
    }
}
