using CodigoActivo.Domain.Constants;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Evaluates whether signup is allowed by the business rules.
/// </summary>
public static class SignupPolicy
{
    /// <summary>
    /// Returns the activity roles available to the specified user type.
    /// </summary>
    /// <param name="userTypeId">Identifier of the user type.</param>
    /// <returns>The items that match the supplied criteria.</returns>
    public static IEnumerable<Guid> SignupRoleIdsFor(Guid userTypeId)
    {
        yield return SeedIds.ActivityRoleTypes.Participant;
        yield return SeedIds.ActivityRoleTypes.Volunteer;
        if (userTypeId == SeedIds.UserTypes.Member)
        {
            yield return SeedIds.ActivityRoleTypes.Leader;
        }
    }

    /// <summary>
    /// Determines whether signup role allowed.
    /// </summary>
    /// <param name="userTypeId">Identifier of the user type.</param>
    /// <param name="roleTypeId">Identifier of the role type.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsSignupRoleAllowed(Guid userTypeId, Guid roleTypeId)
    {
        return SignupRoleIdsFor(userTypeId).Contains(roleTypeId);
    }

    /// <summary>
    /// Determines whether decision.
    /// </summary>
    /// <param name="statusId">Identifier of the status.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsDecision(Guid statusId)
    {
        return statusId == SeedIds.AssignmentStatusTypes.Confirmed
            || statusId == SeedIds.AssignmentStatusTypes.Denied;
    }
}
