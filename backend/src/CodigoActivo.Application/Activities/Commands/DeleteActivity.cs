using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to delete the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record DeleteActivityCommand(ActivityId ActivityId)
    : ICommand<Result>,
        IRoutineCommand;

/// <summary>
/// Executes the command to delete the activity with its signups, keeping their data history. An
/// editor may delete it only while nobody is signed up to it, since signups belong to the people
/// who made them. Its thumbnail is removed after the commit when nothing else uses it.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="eraser">Service that commits the removal with its data history.</param>
public sealed class DeleteActivityCommandHandler(
    IActivityRepository activities,
    ICurrentUser currentUser,
    ParticipationEraser eraser
) : ICommandHandler<DeleteActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, <see cref="ApplicationErrorCode.ActivityNotFound"/>,
    /// or <see cref="ApplicationErrorCode.ParticipationDeletionRequiresAdministrator"/> when
    /// someone other than an administrator deletes an activity with signups.
    /// </returns>
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

        var deletion = ParticipationDeletion.OfActivity(activity);
        if (!currentUser.IsAdmin && await eraser.TakesParticipationAsync(deletion, ct))
        {
            return Error.Forbidden(ApplicationErrorCode.ParticipationDeletionRequiresAdministrator);
        }

        activity.Delete();
        activities.Remove(activity);
        await eraser.EraseAsync(deletion, ct);
        return Result.Success();
    }
}
