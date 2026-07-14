using CodigoActivo.Application.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.API.Caching;

public sealed class HttpCacheInvalidator(HybridCache cache, IOutputCacheStore outputCache)
    : ICacheInvalidator
{
    public async ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags)
    {
        await cache.RemoveByTagAsync(tags, CancellationToken.None);
        foreach (var tag in tags)
            await outputCache.EvictByTagAsync(tag, CancellationToken.None);
    }
}
