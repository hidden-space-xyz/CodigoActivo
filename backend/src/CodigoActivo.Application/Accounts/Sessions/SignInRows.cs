using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Sessions;

/// <summary>
/// Read-side conditions of the sign-in cookies, shared by the queries that issue and check them so
/// they never disagree on which accounts may hold one.
/// </summary>
internal static class SignInRows
{
    /// <summary>
    /// Gets the condition of an account that may hold a challenge or session cookie: an active
    /// account with a password that wrong passwords have not locked.
    /// </summary>
    public static Expression<Func<UserRow, bool>> MayHoldTicket { get; } =
        user =>
            user.UserStatusTypeId == CatalogIds.UserStatuses.IdOf(UserStatus.Active)
            && user.PasswordHash != null
            && user.PasswordLockedAt == null;

    /// <summary>
    /// Builds the condition of the account that has a second-factor challenge open and still young
    /// enough to be answered. Together with <see cref="MayHoldTicket"/> it mirrors
    /// <see cref="User.HasOpenLoginChallenge"/>.
    /// </summary>
    /// <param name="challenge">Challenge named by the cookie.</param>
    /// <param name="openedAfter">Earliest moment the challenge may have been opened.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<UserRow, bool>> HasOpenChallenge(
        LoginChallengeKey challenge,
        DateTimeOffset openedAfter
    )
    {
        var userId = challenge.UserId.Value;
        Guid? challengeId = challenge.ChallengeId.Value;
        return user =>
            user.Id == userId
            && user.LoginChallengeId == challengeId
            && user.LoginChallengeStartedAt > openedAfter;
    }
}
