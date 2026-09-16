namespace CodigoActivo.Application.Caching;

/// <summary>
/// Defines memory limits shared by the application and HTTP output caches.
/// </summary>
public static class CacheLimits
{
    /// <summary>
    /// Maximum total size of each in-memory cache.
    /// </summary>
    public const long LocalCacheSizeBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Maximum size of an individual cached value.
    /// </summary>
    public const long MaximumPayloadBytes = 1024 * 1024;
}
