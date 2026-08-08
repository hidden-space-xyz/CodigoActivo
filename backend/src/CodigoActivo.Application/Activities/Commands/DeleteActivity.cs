using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record DeleteActivityCommand(Guid ActivityId) : ICommand<Result>;

public sealed class DeleteActivityCommandHandler(
    IActivityRepository activities,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteActivityCommand, Result>
{
    public async Task<Result> HandleAsync(
        DeleteActivityCommand command,
        CancellationToken ct = default
    )
    {
        var activity = await activities.FindAsync(a => a.Id == command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        activities.Remove(activity);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        await orphanCleaner.DeleteIfOrphanedAsync(activity.ThumbnailId, ct);
        return Result.Success();
    }
}
