namespace CodigoActivo.Application.Common.Caching;

/// <summary>
/// Query whose result may be served from the application cache: immutable catalogs and
/// non-personal aggregates only. The caching decorator keys the entry with
/// <see cref="CacheKey"/> and evicts it when any of <see cref="Tags"/> is invalidated.
/// </summary>
public interface ICachedQuery
{
    /// <summary>
    /// Gets how long the result stays cached.
    /// </summary>
    public CacheDuration Duration { get; }

    /// <summary>
    /// Gets the tags whose invalidation evicts the result.
    /// </summary>
    public IReadOnlyCollection<string> Tags { get; }

    /// <summary>
    /// Builds the key of the cached result.
    /// </summary>
    /// <param name="today">Current day, for queries whose defaults depend on it.</param>
    /// <returns>The key that identifies the result.</returns>
    public string CacheKey(DateOnly today);
}
