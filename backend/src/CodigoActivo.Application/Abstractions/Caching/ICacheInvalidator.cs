namespace CodigoActivo.Application.Abstractions.Caching;

/// <summary>
/// Invalidates application and HTTP output cache entries by tag.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Invalidates the entries of both caches associated with the supplied tags. The eviction is
    /// not cancellable, so a caller that gives up never leaves one cache stale.
    /// </summary>
    /// <param name="tags">Cache tags to evict, the same in both caches.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags);
}
