using CodigoActivo.Application.Abstractions.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.API.Caching;

/// <summary>
/// Invalidates application and HTTP output cache entries by tag.
/// </summary>
/// <param name="cache">Hybrid cache of the cached query results.</param>
/// <param name="outputCache">Store of the HTTP output cache.</param>
public sealed class HttpCacheInvalidator(HybridCache cache, IOutputCacheStore outputCache)
    : ICacheInvalidator
{
    /// <summary>
    /// Invalidates the entries of both caches associated with the supplied tags. The eviction is
    /// not cancellable, so a caller that gives up never leaves one cache stale.
    /// </summary>
    /// <param name="tags">Cache tags to evict, the same in both caches.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags)
    {
        await cache.RemoveByTagAsync(tags, CancellationToken.None);
        foreach (var tag in tags)
        {
            await outputCache.EvictByTagAsync(tag, CancellationToken.None);
        }
    }
}
