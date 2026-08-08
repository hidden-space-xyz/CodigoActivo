using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

public sealed record DeleteEventCommand(Guid EventId) : ICommand<Result>;

public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IOrphanFileCleaner orphanCleaner,
    IQueryExecutor executor,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteEventCommand, Result>
{
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
