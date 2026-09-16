using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Caching;

/// <summary>
/// Defines the shared cache policies used for cache and authorization configuration.
/// </summary>
public static class CachePolicies
{
    /// <summary>
    /// Stores the shared catalog value.
    /// </summary>
    public static readonly HybridCacheEntryOptions Catalog = new()
    {
        Expiration = TimeSpan.FromHours(12),
        LocalCacheExpiration = TimeSpan.FromHours(12),
    };

    /// <summary>
    /// Stores the shared dashboard value.
    /// </summary>
    public static readonly HybridCacheEntryOptions Dashboard = new()
    {
        Expiration = TimeSpan.FromMinutes(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };
}
