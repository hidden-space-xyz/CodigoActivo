using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

public sealed record SetEventFeaturedCommand(Guid EventId) : ICommand<Result<EventResponse>>;

public sealed class SetEventFeaturedCommandHandler(
    IEventRepository events,
    ICacheInvalidator cacheInvalidator,
    GetEventByIdQueryHandler getById
) : ICommandHandler<SetEventFeaturedCommand, Result<EventResponse>>
{
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
