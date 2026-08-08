using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record UpdateActivityCommand(
    Guid ActivityId,
    UpdateActivityRequest Request,
    Guid UserId
) : ICommand<Result<ActivityResponse>>;

public sealed class UpdateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetActivityByIdQueryHandler getById
) : ICommandHandler<UpdateActivityCommand, Result<ActivityResponse>>
{
    public async Task<Result<ActivityResponse>> HandleAsync(
        UpdateActivityCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var activity = await activities.FindWithRoleCapacitiesAsync(command.ActivityId, ct);
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

        activity.Title = request.Title.Trim();
        activity.Description = request.Description;
        activity.Location = request.Location.Trim();
        activity.ActivityModalityTypeId = request.ActivityModalityTypeId;
        activity.ActivityStartsAt = validated.Value.Schedule.StartsAt;
        activity.ActivityEndsAt = validated.Value.Schedule.EndsAt;
        activity.ThumbnailId = request.ThumbnailId;
        activity.UpdatedAt = clock.UtcNow;
        activity.UpdatedBy = command.UserId;

        ActivityValidator.SyncRoleCapacities(activity, validated.Value.Capacities);

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        if (previousThumbnailId != request.ThumbnailId)
        {
            await orphanCleaner.DeleteIfOrphanedAsync(previousThumbnailId, ct);
        }

        return await getById.HandleAsync(new GetActivityByIdQuery(command.ActivityId), ct);
    }
}
