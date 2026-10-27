using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
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
public sealed record DeleteEventCommand(EventId EventId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event together with its activities, their signups and the
/// terms decisions and ratings taken about it, keeping their data history. Its thumbnail, its
/// description images and the thumbnails of its activities are removed after the commit when
/// nothing else uses them.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="eraser">Service that commits the removal with its data history.</param>
public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    ParticipationEraser eraser
) : ICommandHandler<DeleteEventCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or <see cref="ApplicationErrorCode.EventNotFound"/>.
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

        ev.Delete(await activities.ListThumbnailIdsAsync(command.EventId, ct));
        events.Remove(ev);
        await eraser.EraseAsync(ParticipationDeletion.OfEvent(ev.Id), ct);
        return Result.Success();
    }
}
