using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Roles a person may ask for when signing up: everyone may join as participant or volunteer, and
/// members may also lead.
/// </summary>
public static class SignupRoles
{
    /// <summary>
    /// Lists the roles a membership type may ask for.
    /// </summary>
    /// <param name="userTypeId">Membership type of the account.</param>
    /// <returns>The identifiers of the allowed roles.</returns>
    public static IEnumerable<Guid> For(Guid userTypeId)
    {
        yield return SeedIds.ActivityRoleTypes.Participant;
        yield return SeedIds.ActivityRoleTypes.Volunteer;

        if (userTypeId == SeedIds.UserTypes.Member)
        {
            yield return SeedIds.ActivityRoleTypes.Leader;
        }
    }

    /// <summary>
    /// Tells whether a membership type may ask for a role.
    /// </summary>
    /// <param name="userTypeId">Membership type of the account.</param>
    /// <param name="roleTypeId">Role asked for.</param>
    /// <returns><see langword="true"/> when the role is allowed.</returns>
    public static bool Allows(Guid userTypeId, Guid roleTypeId)
    {
        return For(userTypeId).Contains(roleTypeId);
    }
}
