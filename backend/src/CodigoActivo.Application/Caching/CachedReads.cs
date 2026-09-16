using CodigoActivo.Domain.Common;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Caching;

/// <summary>
/// Coordinates cached reads for cached queries.
/// </summary>
public static class CachedReads
{
    /// <summary>
    /// Gets the requested entity.
    /// </summary>
    /// <typeparam name="TResponse">Type used for response.</typeparam>
    /// <param name="cache">Cache used to reuse previously computed results.</param>
    /// <param name="executor">Query executor used to materialize database results.</param>
    /// <param name="key">The key value.</param>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="tag">The tag value.</param>
    /// <param name="notFound">The not found value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a t on success, or an application error on failure.</returns>
    public static async Task<Result<TResponse>> GetEntityAsync<TResponse>(
        this HybridCache cache,
        IQueryExecutor executor,
        string key,
        Func<IQueryable<TResponse>> source,
        string tag,
        ErrorCode notFound,
        CancellationToken ct
    )
        where TResponse : class
    {
        var response = await cache.GetOrCreateAsync(
            key,
            token => new ValueTask<TResponse?>(executor.FirstOrDefaultAsync(source(), token)),
            CachePolicies.PublicContent,
            [tag],
            ct
        );
        return response is null ? (Result<TResponse>)Error.NotFound(notFound) : response;
    }

    /// <summary>
    /// Gets the requested catalog.
    /// </summary>
    /// <typeparam name="TResponse">Type used for response.</typeparam>
    /// <param name="cache">Cache used to reuse previously computed results.</param>
    /// <param name="executor">Query executor used to materialize database results.</param>
    /// <param name="key">The key value.</param>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t items.</returns>
    public static async Task<IReadOnlyList<TResponse>> GetCatalogAsync<TResponse>(
        this HybridCache cache,
        IQueryExecutor executor,
        string key,
        Func<IQueryable<TResponse>> source,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            key,
            token => new ValueTask<IReadOnlyList<TResponse>>(executor.ToListAsync(source(), token)),
            CachePolicies.Catalog,
            [CacheTags.Catalogs],
            ct
        );
    }
}
