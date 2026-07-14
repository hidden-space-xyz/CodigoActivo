using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Caching;

public static class CachePolicies
{
    public static readonly HybridCacheEntryOptions PublicContent = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public static readonly HybridCacheEntryOptions Catalog = new()
    {
        Expiration = TimeSpan.FromHours(12),
        LocalCacheExpiration = TimeSpan.FromHours(12),
    };

    public static readonly HybridCacheEntryOptions Dashboard = new()
    {
        Expiration = TimeSpan.FromMinutes(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };
}
