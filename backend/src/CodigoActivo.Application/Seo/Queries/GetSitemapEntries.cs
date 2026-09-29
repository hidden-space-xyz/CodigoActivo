using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;

namespace CodigoActivo.Application.Seo.Queries;

/// <summary>
/// Requests the public content pages a search engine may index.
/// </summary>
public sealed record GetSitemapEntriesQuery : IQuery<SitemapEntries>;

/// <summary>
/// Lists the published events, news items and site-hosted resources with their last change.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetSitemapEntriesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetSitemapEntriesQuery, SitemapEntries>
{
    /// <summary>
    /// Handles the request to list the indexable content pages.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the indexable content pages.</returns>
    public async Task<SitemapEntries> HandleAsync(
        GetSitemapEntriesQuery query,
        CancellationToken ct = default
    )
    {
        var eventEntries = await executor.ToListAsync(
            readStore.Events.Select(e => new SitemapEntry(e.Id, e.UpdatedAt ?? e.CreatedAt)),
            ct
        );
        var newsEntries = await executor.ToListAsync(
            readStore.News.Select(n => new SitemapEntry(n.Id, n.UpdatedAt ?? n.CreatedAt)),
            ct
        );
        var resourceEntries = await executor.ToListAsync(
            readStore
                .Resources.Where(r => r.Url == null)
                .Select(r => new SitemapEntry(r.Id, r.UpdatedAt ?? r.CreatedAt)),
            ct
        );

        return new SitemapEntries(eventEntries, newsEntries, resourceEntries);
    }
}
