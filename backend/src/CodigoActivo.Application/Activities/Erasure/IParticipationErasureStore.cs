using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities.Erasure;

/// <summary>
/// Storage steps of a removal of participation data. They run inside the transaction of
/// <see cref="ParticipationEraser"/>.
/// </summary>
public interface IParticipationErasureStore
{
    /// <summary>
    /// Locks the rows the removal takes away and those it would take away if they were added now,
    /// so nothing changes them until the removal commits.
    /// </summary>
    /// <param name="deletion">What is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the rows are locked.</returns>
    public Task LockAsync(ParticipationDeletion deletion, CancellationToken ct = default);

    /// <summary>
    /// Tracks, once they are locked, every row the removal takes away besides the removed
    /// aggregate (for an event, its activities with their signups, its terms decisions with their
    /// history and its ratings; for an activity, its signups, including one added after it was
    /// loaded), so their deletion goes through the unit of work and leaves its data history.
    /// </summary>
    /// <param name="deletion">What is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task LoadAsync(ParticipationDeletion deletion, CancellationToken ct = default);
}
