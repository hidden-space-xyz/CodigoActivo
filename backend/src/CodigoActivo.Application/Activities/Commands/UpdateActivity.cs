using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to update the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateActivityCommand(
    Guid ActivityId,
    UpdateActivityRequest Request,
    Guid UserId
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">The validator value.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to update the activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateActivityCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        var validated = await validator.ValidateActivityAsync(
            activity.EventId,
            request.ActivityStartsAt,
            request.ActivityEndsAt,
            request.ThumbnailId,
            request.ActivityModalityTypeId,
            request.RoleCapacities,
            ct
        );
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        var previousThumbnailId = activity.ThumbnailId;

        activity.Update(
            new ActivityDetails(
                request.Title,
                request.Description,
                request.Location,
                request.ActivityModalityTypeId,
                request.ThumbnailId
            ),
            validated.Value.Schedule,
            validated.Value.Capacities,
            command.UserId,
            clock.UtcNow
        );

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        if (previousThumbnailId != request.ThumbnailId)
        {
            await orphanCleaner.DeleteIfOrphanedAsync(previousThumbnailId, ct);
        }

        return Result.Success();
    }
}
