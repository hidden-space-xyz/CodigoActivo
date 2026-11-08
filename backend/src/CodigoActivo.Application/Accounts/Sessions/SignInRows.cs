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
    /// account with a password. A password lock does not end the sessions already open.
    /// </summary>
    public static Expression<Func<UserRow, bool>> MayHoldTicket { get; } =
        user =>
            user.UserStatusTypeId == CatalogIds.UserStatuses.IdOf(UserStatus.Active)
            && user.PasswordHash != null;

    /// <summary>
    /// Builds the condition of the account that has a second-factor challenge open, still young
    /// enough to be answered, while wrong passwords do not lock it. Together with
    /// <see cref="MayHoldTicket"/> it mirrors <see cref="User.HasOpenLoginChallenge"/>.
    /// </summary>
    /// <param name="challenge">Challenge named by the cookie.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long a challenge can be answered after it opens.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<UserRow, bool>> HasOpenChallenge(
        LoginChallengeKey challenge,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        var userId = challenge.UserId.Value;
        Guid? challengeId = challenge.ChallengeId.Value;
        var openedAfter = now - lifetime;
        return user =>
            user.Id == userId
            && user.LoginChallengeId == challengeId
            && user.LoginChallengeStartedAt > openedAfter
            && (user.PasswordLockedUntil == null || user.PasswordLockedUntil <= now);
    }
}
