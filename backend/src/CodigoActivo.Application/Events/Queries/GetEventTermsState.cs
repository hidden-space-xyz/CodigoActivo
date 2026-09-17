using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve the current user's terms state for an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetEventTermsStateQuery(Guid EventId, Guid UserId)
    : IQuery<EventTermsStateResponse>;

/// <summary>
/// Executes the query to retrieve the current user's terms state for an event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventTermsStateQueryHandler(
    IEventRepository events,
    IQueryExecutor executor
) : IQueryHandler<GetEventTermsStateQuery, EventTermsStateResponse>
{
    /// <summary>
    /// Handles the request to retrieve the current user's terms state for an event.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the event's terms state for the current user.</returns>
    public async Task<EventTermsStateResponse> HandleAsync(
        GetEventTermsStateQuery query,
        CancellationToken ct = default
    )
    {
        var documents = await executor.ToListAsync(
            events
                .QueryTermsDocuments()
                .Where(d => d.EventId == query.EventId)
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new
                {
                    d.TermsDocumentId,
                    d.TermsDocument.Name,
                    d.TermsDocument.Description,
                    d.IsRequired,
                    d.DisplayOrder,
                }),
            ct
        );
        if (documents.Count is 0)
        {
            return new EventTermsStateResponse([], false);
        }

        var acceptances = await executor.ToListAsync(
            events
                .QueryTermsAcceptances()
                .Where(a => a.EventId == query.EventId && a.UserId == query.UserId)
                .Select(a => new
                {
                    a.TermsDocumentId,
                    a.Accepted,
                    a.DecidedAt,
                }),
            ct
        );
        var decidedById = acceptances.ToDictionary(a => a.TermsDocumentId);

        var responses = documents
            .Select(d =>
            {
                decidedById.TryGetValue(d.TermsDocumentId, out var decision);
                return new EventTermsDocumentStateResponse(
                    d.TermsDocumentId,
                    d.Name,
                    d.Description,
                    d.IsRequired,
                    d.DisplayOrder,
                    decision?.Accepted,
                    decision?.DecidedAt
                );
            })
            .ToList();

        var signupBlocked = responses.Any(d => d.Required && d.Accepted != true);

        return new EventTermsStateResponse(responses, signupBlocked);
    }
}
