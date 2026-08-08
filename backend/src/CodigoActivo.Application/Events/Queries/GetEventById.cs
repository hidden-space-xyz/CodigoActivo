using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

public sealed record GetEventByIdQuery(Guid EventId) : IQuery<Result<EventResponse>>;

public sealed class GetEventByIdQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetEventByIdQuery, Result<EventResponse>>
{
    public Task<Result<EventResponse>> HandleAsync(
        GetEventByIdQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetEntityAsync(
            executor,
            $"events:id:{query.EventId}",
            () => events.Query().Where(e => e.Id == query.EventId).Select(Projections.Event),
            CacheTags.Events,
            ErrorCode.EventNotFound,
            ct
        );
    }
}
