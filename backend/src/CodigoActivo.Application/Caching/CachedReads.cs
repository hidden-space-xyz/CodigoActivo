using CodigoActivo.Domain.Common;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Caching;

public static class CachedReads
{
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
