using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to list terms documents.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListTermsDocumentsQuery(TermsDocumentListQuery Filters)
    : IQuery<PagedResult<TermsDocumentResponse>>;

/// <summary>
/// Executes the query to list terms documents.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListTermsDocumentsQueryHandler(
    ITermsDocumentRepository termsDocuments,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListTermsDocumentsQuery, PagedResult<TermsDocumentResponse>>
{
    private static readonly SortMap<TermsDocumentResponse> Sort =
        new SortMap<TermsDocumentResponse>()
            .Add("name", t => t.Name)
            .Default("name")
            .Tie(t => t.Id);

    /// <summary>
    /// Handles the request to list terms documents.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged terms document.</returns>
    public async Task<PagedResult<TermsDocumentResponse>> HandleAsync(
        ListTermsDocumentsQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("events:terms-documents", query.Filters),
            token => new ValueTask<PagedResult<TermsDocumentResponse>>(
                FetchAsync(query.Filters, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.TermsDocuments],
            ct
        );
    }

    private Task<PagedResult<TermsDocumentResponse>> FetchAsync(
        TermsDocumentListQuery query,
        CancellationToken ct
    )
    {
        var source = termsDocuments.Query().Select(Projections.TermsDocument);

        source = source.WhereContains(t => t.Name, query.Name);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
