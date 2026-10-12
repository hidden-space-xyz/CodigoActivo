using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Runs a change of the credentials, codes or challenge of an account with its row locked and
/// reloaded, in one transaction that commits the change when it succeeds. Concurrent requests for
/// the same account therefore run one after another, and each one checks the cooldowns, codes and
/// state the previous one stored instead of failing on its write. Callers decide on the account as
/// loaded whether they will act and take the lock only then, so a request that changes nothing
/// costs no lock and its timing does not depend on the lock, and they never hash a password inside
/// it, so the lock is held only for short database work.
/// </summary>
/// <param name="users">Repository that locks and reloads the account.</param>
/// <param name="uow">Unit of work that runs the transaction and commits the change.</param>
public sealed class AccountLock(IUserRepository users, IUnitOfWork uow)
{
    /// <summary>
    /// Locks and reloads the account, then runs the change, which must check the reloaded state
    /// again before acting on it. Changes staged on the account before the call are discarded.
    /// </summary>
    /// <typeparam name="TResult">Result of the change.</typeparam>
    /// <param name="account">Account loaded in the unit of work.</param>
    /// <param name="change">Change to run once the account is locked; it receives the cancellation token.</param>
    /// <param name="whenGone">Result returned, without running the change, when the account no longer exists.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the result of the change, or <paramref name="whenGone"/>.</returns>
    public Task<TResult> RunAsync<TResult>(
        User account,
        Func<CancellationToken, Task<TResult>> change,
        TResult whenGone,
        CancellationToken ct = default
    )
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(change);

        return uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                if (!await users.LockAsync(account, attempt))
                {
                    return whenGone;
                }

                var result = await change(attempt);
                if (result.IsSuccess)
                {
                    await uow.SaveChangesAsync(attempt);
                }

                return result;
            },
            ct
        );
    }
}
