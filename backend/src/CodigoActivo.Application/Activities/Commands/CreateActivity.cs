using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to create an activity.
/// </summary>
/// <param name="EventId">Identifier of the event the activity belongs to.</param>
/// <param name="Activity">Details, schedule and capacities of the new activity.</param>
public sealed record CreateActivityCommand(EventId EventId, ActivityDraft Activity)
    : ICommand<Result<ActivityId>>,
        IRoutineCommand;

/// <summary>
/// Executes the command to create an activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">Validator of the event, schedule, thumbnail and role capacities.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the creator.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class CreateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<CreateActivityCommand, Result<ActivityId>>
{
    /// <summary>
    /// Handles the request to create an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new activity; <c>EventNotFound</c> or
    /// <c>ActivityThumbnailNotFound</c> when either is missing, or the domain error of an invalid
    /// schedule or role capacity plan.
    /// </returns>
    public async Task<Result<ActivityId>> HandleAsync(
        CreateActivityCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.Activity;

        var validated = await validator.ValidateActivityAsync(command.EventId, draft, ct);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        var activity = Activity.Create(
            command.EventId,
            draft.ToDetails(),
            validated.Value.Schedule,
            validated.Value.Capacities,
            currentUser.RequiredId(),
            clock.UtcNow
        );

        await activities.AddAsync(activity, ct);
        return activity.Id;
    }
}
