using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Who is entitled to the early signup: members and sponsors. A dependent is entitled when their
/// guardian is.
/// </summary>
public static class EarlySignup
{
    /// <summary>
    /// Tells whether a membership type is entitled to the early signup.
    /// </summary>
    /// <param name="userTypeId">Membership type of the account, or of the guardian of a dependent.</param>
    /// <returns><see langword="true"/> when the type is entitled.</returns>
    public static bool IsEntitled(Guid userTypeId)
    {
        return userTypeId == SeedIds.UserTypes.Member || userTypeId == SeedIds.UserTypes.Sponsor;
    }

    /// <summary>
    /// Tells whether a person is entitled to the early signup.
    /// </summary>
    /// <param name="person">Account that signs up.</param>
    /// <param name="guardian">Guardian of the account when it is a dependent.</param>
    /// <returns><see langword="true"/> when the person, or the guardian of a dependent, is entitled.</returns>
    public static bool IsEntitled(User person, User? guardian)
    {
        ArgumentNullException.ThrowIfNull(person);

        return IsEntitled(guardian?.UserTypeId ?? person.UserTypeId);
    }
}
