using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities.Erasure;

/// <summary>
/// Commits the removal of participation data staged in the unit of work (an event or an activity
/// removed, or a signup cancelled) in one transaction with the blocked copy the law requires of
/// what it takes away (LOPDGDD article 32): the signups and, for an event, the terms decisions with
/// their signatures and history. A removal that takes away none of them stores no copy.
/// </summary>
/// <param name="archive">Storage steps that lock and copy the participation.</param>
/// <param name="deletedParticipations">Repository of the blocked copies.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class ParticipationEraser(
    IParticipationArchive archive,
    IDeletedParticipationRepository deletedParticipations,
    IUnitOfWork uow
)
{
    /// <summary>
    /// Stores the copy of what the removal takes away and commits it with the changes staged.
    /// </summary>
    /// <param name="deletion">What is removed, by whom and when.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the removal is committed.</returns>
    public Task EraseAsync(ParticipationDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        return uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                await archive.LockAsync(deletion, attempt);
                var copy = await archive.CaptureCopyAsync(deletion, attempt);
                if (copy is not null)
                {
                    await deletedParticipations.AddAsync(
                        DeletedParticipation.Record(deletion, copy),
                        attempt
                    );
                }

                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
    }
}
