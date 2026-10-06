using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to update the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Activity">New details, schedule and capacities of the activity.</param>
public sealed record UpdateActivityCommand(ActivityId ActivityId, ActivityDraft Activity)
    : ICommand<Result>;

/// <summary>
/// Executes the command to update the activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">The validator value.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class UpdateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    ICurrentUser currentUser,
    IClock clock
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
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.Activity;

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        var validated = await validator.ValidateActivityAsync(activity.EventId, draft, ct);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        activity.Update(
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
        return Result.Success();
    }
}
