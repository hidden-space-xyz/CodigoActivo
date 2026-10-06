using CodigoActivo.Domain.Users;

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
    /// <param name="userType">Membership type of the account.</param>
    /// <returns>The allowed roles.</returns>
    public static IEnumerable<ActivityRole> For(UserType userType)
    {
        yield return ActivityRole.Participant;
        yield return ActivityRole.Volunteer;

        if (userType is UserType.Member)
        {
            yield return ActivityRole.Leader;
        }
    }

    /// <summary>
    /// Tells whether a membership type may ask for a role.
    /// </summary>
    /// <param name="userType">Membership type of the account.</param>
    /// <param name="role">Role asked for.</param>
    /// <returns><see langword="true"/> when the role is allowed.</returns>
    public static bool Allows(UserType userType, ActivityRole role)
    {
        return For(userType).Contains(role);
    }
}
