using CodigoActivo.Domain.Common;

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
    /// <returns>A task whose result is the news item, or <see langword="null"/> when it does not exist.</returns>
    public Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the news items featured right now.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the featured news items.</returns>
    public Task<IReadOnlyList<NewsItem>> ListFeaturedAsync(CancellationToken ct = default);
}
