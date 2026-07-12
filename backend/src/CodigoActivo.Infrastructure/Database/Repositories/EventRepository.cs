using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class EventRepository(CodigoActivoDbContext context)
    : Repository<Event>(context),
        IEventRepository
{
    public async Task<Event?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        return await Set.Include(e => e.Categories).FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        return SetExclusiveFeaturedAsync(Set, id, ct);
    }
}
