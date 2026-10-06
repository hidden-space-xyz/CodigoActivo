using CodigoActivo.Domain.News;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.News;

/// <summary>
/// Stores and loads news items.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class NewsItemRepository(CodigoActivoDbContext context)
    : AggregateRepository<NewsItem>(context),
        INewsItemRepository
{
    /// <inheritdoc />
    public Task<NewsItem?> GetByIdAsync(NewsItemId id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(newsItem => newsItem.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsItem>> ListFeaturedAsync(CancellationToken ct = default)
    {
        return await Set.Where(newsItem => newsItem.Featured).ToListAsync(ct);
    }
}
