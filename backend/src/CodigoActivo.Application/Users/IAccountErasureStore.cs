using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users;

/// <summary>
/// Storage steps of an account erasure that work on the whole household at once. They run inside
/// the transaction of <see cref="AccountEraser"/>.
/// </summary>
public interface IAccountErasureStore
{
    /// <summary>
    /// Locks the account, its dependents and the rows copied about them, so nothing changes them
    /// until the erasure commits.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account no longer exists.</returns>
    public Task<bool> LockHouseholdAsync(UserId accountId, CancellationToken ct = default);

    /// <summary>
    /// Builds the legal copy of the account and its household as they stand now.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="erasure">Who erases it and when.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the JSON document of the copy.</returns>
    public Task<string> CaptureLegalCopyAsync(
        UserId accountId,
        AccountErasure erasure,
        CancellationToken ct = default
    );

    /// <summary>
    /// Credits to another account the content the household created or last edited.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="heirId">Identifier of the account that inherits the content.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the content is credited to the heir.</returns>
    public Task HandOverAuthoredContentAsync(
        UserId accountId,
        UserId heirId,
        CancellationToken ct = default
    );
}
