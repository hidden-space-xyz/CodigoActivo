using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
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
/// Executes the command to delete the event together with its activities.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
public sealed class DeleteEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities
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
        return Result.Success();
    }
}
