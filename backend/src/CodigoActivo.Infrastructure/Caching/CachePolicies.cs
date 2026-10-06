using CodigoActivo.Application.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Infrastructure.Caching;

/// <summary>
/// Expiration of the application cache entries for each <see cref="CacheDuration"/>.
/// </summary>
public static class CachePolicies
{
    /// <summary>
    /// Gets the entry options of immutable catalogs.
    /// </summary>
    public static HybridCacheEntryOptions Catalog { get; } =
        new()
        {
            Expiration = TimeSpan.FromHours(12),
            LocalCacheExpiration = TimeSpan.FromHours(12),
        };

    /// <summary>
    /// Gets the entry options of dashboard aggregates.
    /// </summary>
    public static HybridCacheEntryOptions Dashboard { get; } =
        new()
        {
            Expiration = TimeSpan.FromMinutes(1),
            LocalCacheExpiration = TimeSpan.FromMinutes(1),
        };

    /// <summary>
    /// Gets the entry options of a duration.
    /// </summary>
    /// <param name="duration">How long the result lives.</param>
    /// <returns>The entry options.</returns>
    public static HybridCacheEntryOptions For(CacheDuration duration)
    {
        return duration switch
        {
            CacheDuration.Catalog => Catalog,
            CacheDuration.Dashboard => Dashboard,
            _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null),
        };
    }
}
