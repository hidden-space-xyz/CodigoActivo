using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to create an activity.
/// </summary>
/// <param name="EventId">Identifier of the event the activity belongs to.</param>
/// <param name="Activity">Details, schedule and capacities of the new activity.</param>
public sealed record CreateActivityCommand(EventId EventId, ActivityDraft Activity)
    : ICommand<Result<ActivityId>>;

/// <summary>
/// Executes the command to create an activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">The validator value.</param>
/// <param name="currentUser">Person the use case runs for.</param>
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
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
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
            new ActivityDetails(
                draft.Title,
                draft.Description,
                draft.Location,
                validated.Value.Modality,
                draft.ThumbnailId
            ),
            validated.Value.Schedule,
            validated.Value.Capacities,
            currentUser.RequiredId(),
            clock.UtcNow
        );

        await activities.AddAsync(activity, ct);
        return activity.Id;
    }
}
