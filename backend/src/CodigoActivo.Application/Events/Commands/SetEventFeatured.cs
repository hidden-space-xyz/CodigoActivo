using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to set event featured.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record SetEventFeaturedCommand(Guid EventId) : ICommand<Result<EventResponse>>;

/// <summary>
/// Executes the command to set event featured.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve event by identifier.</param>
public sealed class SetEventFeaturedCommandHandler(
    IEventRepository events,
    ICacheInvalidator cacheInvalidator,
    GetEventByIdQueryHandler getById
) : ICommandHandler<SetEventFeaturedCommand, Result<EventResponse>>
{
    /// <summary>
    /// Handles the request to set event featured.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event on success, or an application error on failure.</returns>
    public async Task<Result<EventResponse>> HandleAsync(
        SetEventFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        if (!await events.SetFeaturedAsync(command.EventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Events);
        return await getById.HandleAsync(new GetEventByIdQuery(command.EventId), ct);
    }
}
