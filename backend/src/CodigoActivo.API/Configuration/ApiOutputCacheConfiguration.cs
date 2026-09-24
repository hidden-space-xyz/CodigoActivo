using CodigoActivo.API.Caching;
using CodigoActivo.Application.Caching;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Configuration;

internal static class ApiOutputCacheConfiguration
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    internal static void AddApiOutputCaching(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.SizeLimit = CacheLimits.LocalCacheSizeBytes;
            options.MaximumBodySize = CacheLimits.MaximumPayloadBytes;

            foreach (var tag in CacheTags.OutputCached)
            {
                options.AddPolicy(tag, policy => policy.Expire(CacheLifetime).Tag(tag));
            }

            options.AddPolicy(
                OutputCachePolicies.Seo,
                policy =>
                    policy
                        .Expire(CacheLifetime)
                        .Tag(CacheTags.Events, CacheTags.News, CacheTags.Resources)
            );
        });

        services.AddSingleton<ICacheInvalidator, HttpCacheInvalidator>();
    }
}
