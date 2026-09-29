using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to create an activity.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateActivityCommand(Guid EventId, CreateActivityRequest Request, Guid UserId)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to create an activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">The validator value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateActivityCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created activity, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
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

        var activity = Activity.Create(
            command.EventId,
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

        await activities.AddAsync(activity, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        return activity.Id;
    }
}
