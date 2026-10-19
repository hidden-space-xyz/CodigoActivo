using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to list the terms decisions taken about an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventTermsDecisionsQuery(
    EventId EventId,
    EventTermsDecisionListQuery Filters
) : IQuery<Result<PagedResult<EventTermsDecisionResponse>>>;

/// <summary>
/// Executes the query to list the terms decisions taken about an event, latest first. Declined
/// required documents are never stored, so they are not listed.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListEventTermsDecisionsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListEventTermsDecisionsQuery, Result<PagedResult<EventTermsDecisionResponse>>>
{
    private static readonly SortMap<EventTermsDecisionResponse> Sort =
        new SortMap<EventTermsDecisionResponse>()
            .Add("decidedAt", d => d.DecidedAt)
            .Add("firstName", d => d.FirstName)
            .Add("lastName", d => d.LastName)
            .Add("document", d => d.DocumentName)
            .Default("-decidedAt")
            .Tie(d => d.UserId)
            .Tie(d => d.TermsDocumentId);

    /// <summary>
    /// Handles the request to list the terms decisions taken about an event.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a page of decisions on success, or an application error when the event does not exist.</returns>
    public async Task<Result<PagedResult<EventTermsDecisionResponse>>> HandleAsync(
        ListEventTermsDecisionsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        if (!await executor.AnyAsync(readStore.Events.Where(e => e.Id == eventId), ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var filters = query.Filters;
        var termsDocumentId = filters.TermsDocumentId;
        var source = readStore
            .EventTermsAcceptances.Where(a => a.EventId == eventId)
            .Where(a => termsDocumentId == null || a.TermsDocumentId == termsDocumentId)
            .WhereContains(
                a =>
                    a.User.FirstName
                    + " "
                    + a.User.LastName
                    + " "
                    + (a.User.Email ?? "")
                    + " "
                    + a.DecidedBy.FirstName
                    + " "
                    + a.DecidedBy.LastName
                    + " "
                    + (a.DecidedBy.Email ?? ""),
                filters.Search
            )
            .Select(a => new EventTermsDecisionResponse
            {
                UserId = a.UserId,
                FirstName = a.User.FirstName,
                LastName = a.User.LastName,
                Email = a.User.Email,
                DecidedById = a.DecidedById,
                DecidedByFirstName = a.DecidedBy.FirstName,
                DecidedByLastName = a.DecidedBy.LastName,
                TermsDocumentId = a.TermsDocumentId,
                DocumentName = a.TermsDocument.Name,
                DocumentKind = a.TermsDocument.Kind,
                Accepted = a.Accepted,
                Withdrawn = !a.Accepted && a.Changes.Any(change => change.Accepted),
                DecidedAt = a.DecidedAt,
                HasSignature = a.Signature != null,
            });

        return await executor.ToPagedAsync(
            Sort.Apply(source, filters.Sort),
            filters.Page,
            filters.PageSize,
            ct
        );
    }
}
