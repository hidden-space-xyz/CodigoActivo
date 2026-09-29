using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Stores the legal copies of erased accounts.
/// </summary>
public interface IDeletedAccountRepository : IRepository<DeletedAccount>
{
    /// <summary>
    /// Physically removes the copies of the accounts deleted up to a moment. It runs immediately,
    /// without waiting for the unit of work.
    /// </summary>
    /// <param name="deletedUpTo">Latest deletion moment whose copy is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of copies removed.</returns>
    public Task<int> RemoveDeletedUpToAsync(
        DateTimeOffset deletedUpTo,
        CancellationToken ct = default
    );
}
