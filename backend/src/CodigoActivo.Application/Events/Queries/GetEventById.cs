using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve event by identifier.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventByIdQuery(EventId EventId) : IQuery<Result<EventResponse>>;

/// <summary>
/// Executes the query to retrieve event by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock that places the event in its stage.</param>
public sealed class GetEventByIdQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetEventByIdQuery, Result<EventResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event on success, or an application error on failure.</returns>
    public async Task<Result<EventResponse>> HandleAsync(
        GetEventByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore.Events.Where(e => e.Id == query.EventId.Value).Select(EventProjections.Event),
            ct
        );
        return response is null
            ? Error.NotFound(ApplicationErrorCode.EventNotFound)
            : EventStages.Stamp(response, clock.UtcNow, clock.Today);
    }
}
