using CodigoActivo.Application.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.API.Caching;

/// <summary>
/// Invalidates application and HTTP output cache entries by tag.
/// </summary>
/// <param name="cache">Cache used to reuse previously computed results.</param>
/// <param name="outputCache">The output cache value.</param>
public sealed class HttpCacheInvalidator(HybridCache cache, IOutputCacheStore outputCache)
    : ICacheInvalidator
{
    /// <summary>
    /// Invalidates cached entries associated with the supplied tags.
    /// </summary>
    /// <param name="tags">The tags value.</param>
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
