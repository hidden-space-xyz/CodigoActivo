namespace CodigoActivo.Application.Caching;

/// <summary>
/// Invalidates application and HTTP output cache entries by tag.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Invalidates cached entries associated with the supplied tags.
    /// </summary>
    /// <param name="tags">The tags value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags);
}
