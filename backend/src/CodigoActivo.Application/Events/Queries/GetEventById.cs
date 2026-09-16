using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve event by identifier.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventByIdQuery(Guid EventId) : IQuery<Result<EventResponse>>;

/// <summary>
/// Executes the query to retrieve event by identifier.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetEventByIdQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetEventByIdQuery, Result<EventResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event on success, or an application error on failure.</returns>
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
