using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves announcement data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class AnnouncementRepository(CodigoActivoDbContext context)
    : Repository<Announcement>(context),
        IAnnouncementRepository
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
