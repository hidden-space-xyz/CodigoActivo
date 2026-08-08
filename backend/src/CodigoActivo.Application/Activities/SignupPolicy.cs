using CodigoActivo.Domain.Constants;

namespace CodigoActivo.Application.Activities;

public static class SignupPolicy
{
    public static IEnumerable<Guid> SignupRoleIdsFor(Guid userTypeId)
    {
        yield return SeedIds.ActivityRoleTypes.Participant;
        yield return SeedIds.ActivityRoleTypes.Volunteer;
        if (userTypeId == SeedIds.UserTypes.Member)
        {
            yield return SeedIds.ActivityRoleTypes.Leader;
        }
    }

    public static bool IsSignupRoleAllowed(Guid userTypeId, Guid roleTypeId)
    {
        return SignupRoleIdsFor(userTypeId).Contains(roleTypeId);
    }

    public static bool IsDecision(Guid statusId)
    {
        return statusId == SeedIds.AssignmentStatusTypes.Confirmed
            || statusId == SeedIds.AssignmentStatusTypes.Denied;
    }
}
