using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities.Erasure;

/// <summary>
/// Commits the removal of participation data staged in the unit of work (an event, an activity or
/// a rating removed, or a signup cancelled) in one save that first tracks what it takes away, so
/// every deleted signup, rating and terms decision leaves its data history, which the law requires
/// to keep blocked (LOPDGDD article 32). A withdrawn consent it deletes before the association's
/// legal contact was told about it is told first, and so is every consent given it deletes about
/// an event that has already started.
/// </summary>
/// <param name="store">Storage steps of the removal.</param>
/// <param name="withdrawalNotices">Notifier that tells the legal contact about withdrawals.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class ParticipationEraser(
    IParticipationErasureStore store,
    ConsentWithdrawalNotifier withdrawalNotices,
    IUnitOfWork uow
)
{
    /// <summary>
    /// Commits the removal staged in the unit of work.
    /// </summary>
    /// <param name="deletion">What is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EraseAsync(ParticipationDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        await store.LoadAsync(deletion, ct);
        await withdrawalNotices.NotifyAsync(
            await store.ListWithdrawalNoticesPendingAsync(deletion, ct),
            ct
        );
        await withdrawalNotices.NotifyErasedAsync(
            await store.ListAcceptedDecisionsAsync(deletion, ct),
            ct
        );
        await uow.SaveChangesAsync(ct);
    }
}
