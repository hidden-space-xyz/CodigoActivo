using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// A guardian and the dependents in their care. The size of a household is bounded, so one account
/// cannot fill activities, rosters and lists with an unlimited number of people.
/// </summary>
public static class Household
{
    /// <summary>
    /// Most dependents a guardian may have, registration included.
    /// </summary>
    public const int MaxDependents = 20;

    /// <summary>
    /// Most people a household has: the guardian and their dependents.
    /// </summary>
    public const int MaxMembers = MaxDependents + 1;

    /// <summary>
    /// Checks that a guardian may add another dependent.
    /// </summary>
    /// <param name="dependents">Dependents the guardian has now.</param>
    /// <returns>Success, or a conflict once the guardian has <see cref="MaxDependents"/> dependents.</returns>
    public static Result EnsureMayAddDependent(int dependents)
    {
        return dependents < MaxDependents
            ? Result.Success()
            : Error.Conflict(ErrorCode.UserChildLimitReached);
    }
}
