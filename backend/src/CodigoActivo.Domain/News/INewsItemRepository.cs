namespace CodigoActivo.Domain.News;

/// <summary>
/// Stores and loads news items.
/// </summary>
public interface INewsItemRepository : IRepository<NewsItem>
{
    /// <summary>
    /// Loads a news item to change it.
    /// </summary>
    /// <param name="id">Identifier of the news item.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the news item, or <see langword="null"/> when it does not exist.
    /// </returns>
    public Task<NewsItem?> GetByIdAsync(NewsItemId id, CancellationToken ct = default);

    /// <summary>
    /// Loads the news item featured right now; a unique index keeps it to one at most.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the featured news item, or <see langword="null"/> when none is.
    /// </returns>
    public Task<NewsItem?> GetFeaturedAsync(CancellationToken ct = default);
}
