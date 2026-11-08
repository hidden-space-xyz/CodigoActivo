using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to delete the event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record DeleteEventCommand(EventId EventId) : ICommand<Result>, IRoutineCommand;

/// <summary>
/// Executes the command to delete the event together with its activities, their signups and the
/// terms decisions and ratings taken about it, keeping their data history. An editor may delete
/// it only while it has none of them, since they belong to the people who made them. Its
/// thumbnail, its description images and the thumbnails of its activities are removed after the
/// commit when nothing else uses them.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="eraser">Service that commits the removal with its data history.</param>
public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    ICurrentUser currentUser,
    ParticipationEraser eraser
) : ICommandHandler<DeleteEventCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, <see cref="ApplicationErrorCode.EventNotFound"/>, or
    /// <see cref="ApplicationErrorCode.ParticipationDeletionRequiresAdministrator"/> when someone
    /// other than an administrator deletes an event with signups, terms decisions or ratings.
    /// </returns>
    public async Task<Result> HandleAsync(
        DeleteEventCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var ev = await events.GetByIdAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var deletion = ParticipationDeletion.OfEvent(ev.Id);
        if (!currentUser.IsAdmin && await eraser.TakesParticipationAsync(deletion, ct))
        {
            return Error.Forbidden(ApplicationErrorCode.ParticipationDeletionRequiresAdministrator);
        }

        ev.Delete(await activities.ListThumbnailIdsAsync(command.EventId, ct));
        events.Remove(ev);
        await eraser.EraseAsync(deletion, ct);
        return Result.Success();
    }
}
