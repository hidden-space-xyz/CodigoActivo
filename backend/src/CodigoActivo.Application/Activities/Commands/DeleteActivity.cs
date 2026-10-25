using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to delete the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record DeleteActivityCommand(ActivityId ActivityId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the activity with its signups, keeping their data history.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="eraser">Service that commits the removal with its data history.</param>
public sealed class DeleteActivityCommandHandler(
    IActivityRepository activities,
    ParticipationEraser eraser
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
        ArgumentNullException.ThrowIfNull(command);

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        activity.Delete();
        activities.Remove(activity);
        await eraser.EraseAsync(ParticipationDeletion.OfActivity(activity), ct);
        return Result.Success();
    }
}
