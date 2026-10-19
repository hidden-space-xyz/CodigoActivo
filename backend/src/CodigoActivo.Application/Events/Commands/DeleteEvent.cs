using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to delete the event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record DeleteEventCommand(EventId EventId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event together with its activities, their signups and the
/// terms decisions taken about it, keeping their blocked copy first.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="eraser">Service that commits the removal with the blocked copy.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    ParticipationEraser eraser,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<DeleteEventCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
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

        ev.Delete(await activities.ListThumbnailIdsAsync(command.EventId, ct));
        events.Remove(ev);
        await eraser.EraseAsync(
            ParticipationDeletion.OfEvent(ev.Id, currentUser.RequiredId(), clock.UtcNow),
            ct
        );
        return Result.Success();
    }
}
