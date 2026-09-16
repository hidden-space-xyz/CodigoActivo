using System.Text.Json;

namespace CodigoActivo.Application.Caching;

/// <summary>
/// Defines the shared cache keys used for cache and authorization configuration.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Builds a stable cache key from a prefix and serialized query.
    /// </summary>
    /// <typeparam name="TQuery">Type of query handled by the component.</typeparam>
    /// <param name="prefix">The prefix value.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <returns>The generated text.</returns>
    public static string For<TQuery>(string prefix, TQuery query)
    {
        return $"{prefix}:{JsonSerializer.Serialize(query)}";
    }
}
