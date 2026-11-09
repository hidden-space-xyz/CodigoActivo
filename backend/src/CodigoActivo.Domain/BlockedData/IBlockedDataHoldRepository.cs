using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Stores the holds on the blocked data.
/// </summary>
public interface IBlockedDataHoldRepository : IRepository<BlockedDataHold>
{
    /// <summary>
    /// Loads a hold.
    /// </summary>
    /// <param name="id">Identifier of the hold.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the hold, or <see langword="null"/> when missing.</returns>
    public Task<BlockedDataHold?> GetByIdAsync(
        BlockedDataHoldId id,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists the accounts whose history an active hold keeps.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains each account once.</returns>
    public Task<IReadOnlyList<UserId>> ListHeldAccountIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Physically removes the holds lifted up to a moment. It runs immediately, without waiting
    /// for the unit of work.
    /// </summary>
    /// <param name="liftedUpTo">Latest lifting moment whose hold is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of holds removed.</returns>
    public Task<int> PurgeLiftedUpToAsync(
        DateTimeOffset liftedUpTo,
        CancellationToken ct = default
    );
}
