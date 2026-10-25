using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Erasure;

/// <summary>
/// Storage steps of an account erasure that work on the whole household at once. They run inside
/// the transaction of <see cref="AccountEraser"/>.
/// </summary>
public interface IAccountErasureStore
{
    /// <summary>
    /// Locks the account, its dependents and the rows that belong to them, so nothing changes them
    /// until the erasure commits.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account no longer exists.</returns>
    public Task<bool> LockHouseholdAsync(UserId accountId, CancellationToken ct = default);

    /// <summary>
    /// Tracks every row the erasure deletes (the dependents, the signups, the terms decisions with
    /// their history, the ratings and the news consent changes of the household), so their
    /// deletion goes through the unit of work and leaves its data history.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task LoadHouseholdAsync(UserId accountId, CancellationToken ct = default);

    /// <summary>
    /// Credits another account instead of the household as author, last editor or uploader of
    /// content and as the account that recorded a terms decision about someone else. The data
    /// history keeps the original credit.
    /// </summary>
    /// <param name="accountId">Identifier of the account being erased.</param>
    /// <param name="heirId">Identifier of the account credited instead.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task HandOverAuthoredContentAsync(
        UserId accountId,
        UserId heirId,
        CancellationToken ct = default
    );
}
