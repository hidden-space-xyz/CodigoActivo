using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to delete the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record DeleteActivityCommand(Guid ActivityId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteActivityCommandHandler(
    IActivityRepository activities,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
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
