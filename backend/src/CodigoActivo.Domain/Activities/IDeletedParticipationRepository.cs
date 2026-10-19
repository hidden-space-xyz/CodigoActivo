using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Stores the blocked copies of removed participation.
/// </summary>
public interface IDeletedParticipationRepository : IRepository<DeletedParticipation>
{
    /// <summary>
    /// Physically removes the copies of the participation removed up to a moment. It runs
    /// immediately, without waiting for the unit of work.
    /// </summary>
    /// <param name="deletedUpTo">Latest removal moment whose copy is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of copies removed.</returns>
    public Task<int> PurgeDeletedUpToAsync(
        DateTimeOffset deletedUpTo,
        CancellationToken ct = default
    );
}
