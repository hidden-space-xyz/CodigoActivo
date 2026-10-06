using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Stores the sessions opened by accounts. Ending sessions runs immediately, without waiting for
/// the unit of work.
/// </summary>
public interface IUserSessionRepository : IRepository<UserSession>
{
    /// <summary>
    /// Ends one session of an account.
    /// </summary>
    /// <param name="sessionId">Identifier of the session.</param>
    /// <param name="userId">Identifier of the account that owns it.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the session no longer exists.</returns>
    public Task EndAsync(UserSessionId sessionId, UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Ends every session of an account.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the account has no sessions.</returns>
    public Task EndAllAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Removes the sessions that stopped being accepted by a moment.
    /// </summary>
    /// <param name="now">Moment the sessions are checked against.</param>
    /// <param name="userId">Account whose sessions are removed; every account when omitted.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of sessions removed.</returns>
    public Task<int> RemoveExpiredAsync(
        DateTimeOffset now,
        UserId? userId = null,
        CancellationToken ct = default
    );
}
