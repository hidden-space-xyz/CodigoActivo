using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.TermsDocuments.Contracts;

namespace CodigoActivo.Application.TermsDocuments.Queries;

/// <summary>
/// Carries the criteria used to list terms documents.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListTermsDocumentsQuery(TermsDocumentListQuery Filters)
    : IQuery<PagedResult<TermsDocumentResponse>>;

/// <summary>
/// Executes the query to list terms documents.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListTermsDocumentsQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListTermsDocumentsQuery, PagedResult<TermsDocumentResponse>>
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
    public Task<PagedResult<TermsDocumentResponse>> HandleAsync(
        ListTermsDocumentsQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(query.Filters, ct);
    }

    private Task<PagedResult<TermsDocumentResponse>> FetchAsync(
        TermsDocumentListQuery query,
        CancellationToken ct
    )
    {
        var source = readStore.TermsDocuments.Select(TermsDocumentProjections.TermsDocument);

        source = source.WhereContains(t => t.Name, query.Name);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
