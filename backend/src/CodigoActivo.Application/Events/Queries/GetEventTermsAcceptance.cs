using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve event terms acceptance.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetEventTermsAcceptanceQuery(Guid EventId, Guid UserId)
    : IQuery<EventTermsAcceptanceResponse>;

/// <summary>
/// Executes the query to retrieve event terms acceptance.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventTermsAcceptanceQueryHandler(
    IEventRepository events,
    IQueryExecutor executor
) : IQueryHandler<GetEventTermsAcceptanceQuery, EventTermsAcceptanceResponse>
{
    /// <summary>
    /// Handles the request to retrieve event terms acceptance.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event terms acceptance.</returns>
    public async Task<EventTermsAcceptanceResponse> HandleAsync(
        GetEventTermsAcceptanceQuery query,
        CancellationToken ct = default
    )
    {
        var termsDocumentId = await executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == query.EventId).Select(e => e.TermsDocumentId),
            ct
        );
        return new EventTermsAcceptanceResponse(
            termsDocumentId is { } id
                && await events.TermsAcceptanceExistsAsync(query.EventId, query.UserId, id, ct)
        );
    }
}
