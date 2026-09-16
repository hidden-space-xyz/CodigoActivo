using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to create an activity.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateActivityCommand(Guid EventId, CreateActivityRequest Request, Guid UserId)
    : ICommand<Result<ActivityResponse>>;

/// <summary>
/// Executes the command to create an activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">The validator value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve activity by identifier.</param>
public sealed class CreateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetActivityByIdQueryHandler getById
) : ICommandHandler<CreateActivityCommand, Result<ActivityResponse>>
{
    /// <summary>
    /// Handles the request to create an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an activity on success, or an application error on failure.</returns>
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
