using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to delete the event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record DeleteEventCommand(Guid EventId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IOrphanFileCleaner orphanCleaner,
    IQueryExecutor executor,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteEventCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteEventCommand command,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var activityThumbnailIds = await executor.ToListAsync(
            activities.Query().Where(a => a.EventId == command.EventId).Select(a => a.ThumbnailId),
            ct
        );

        events.Remove(ev);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Events, CacheTags.Activities);

        var orphanCandidates = activityThumbnailIds
            .Append(ev.ThumbnailId)
            .Concat(RichTextFileReferences.Extract(ev.Description))
            .Distinct()
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
