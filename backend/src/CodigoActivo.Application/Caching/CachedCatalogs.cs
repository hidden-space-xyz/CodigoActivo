using CodigoActivo.Domain.Common;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Caching;

/// <summary>
/// Coordinates cached reads for immutable catalogs.
/// </summary>
public static class CachedCatalogs
{
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
