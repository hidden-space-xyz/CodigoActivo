using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record CreateActivityCommand(Guid EventId, CreateActivityRequest Request, Guid UserId)
    : ICommand<Result<ActivityResponse>>;

public sealed class CreateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetActivityByIdQueryHandler getById
) : ICommandHandler<CreateActivityCommand, Result<ActivityResponse>>
{
    public async Task<Result<ActivityResponse>> HandleAsync(
        CreateActivityCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var validated = await validator.ValidateActivityAsync(
            command.EventId,
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

        var activity = new Activity
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Location = request.Location.Trim(),
            ActivityModalityTypeId = request.ActivityModalityTypeId,
            ActivityStartsAt = validated.Value.Schedule.StartsAt,
            ActivityEndsAt = validated.Value.Schedule.EndsAt,
            EventId = command.EventId,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
            RoleCapacities =
            [
                .. validated.Value.Capacities.Select(item => new ActivityRoleCapacity
                {
                    ActivityRoleTypeId = item.RoleTypeId,
                    DesiredCount = item.DesiredCount,
                }),
            ],
        };

        await activities.AddAsync(activity, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        return await getById.HandleAsync(new GetActivityByIdQuery(activity.Id), ct);
    }
}
