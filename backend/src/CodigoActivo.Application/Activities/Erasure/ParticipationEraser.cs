using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities.Erasure;

/// <summary>
/// Commits the removal of participation data staged in the unit of work (an event, an activity or
/// a rating removed, or a signup cancelled) in one transaction that first locks what it takes away
/// and tracks it, so every deleted signup, rating and terms decision leaves its data history,
/// which the law requires to keep blocked (LOPDGDD article 32).
/// </summary>
/// <param name="store">Storage steps of the removal.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class ParticipationEraser(IParticipationErasureStore store, IUnitOfWork uow)
{
    /// <summary>
    /// Commits the removal staged in the unit of work.
    /// </summary>
    /// <param name="deletion">What is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task EraseAsync(ParticipationDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        return uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                await store.LockAsync(deletion, attempt);
                await store.LoadAsync(deletion, attempt);
                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
    }
}
