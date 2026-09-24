using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves news item data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class NewsItemRepository(CodigoActivoDbContext context)
    : Repository<NewsItem>(context),
        INewsItemRepository
{
    /// <summary>
    /// Sets the featured state.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        return SetExclusiveFeaturedAsync(Set, id, ct);
    }
}
