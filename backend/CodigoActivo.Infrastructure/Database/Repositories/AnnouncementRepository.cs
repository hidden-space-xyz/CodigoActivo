using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class AnnouncementRepository(CodigoActivoDbContext context)
    : Repository<Announcement>(context),
        IAnnouncementRepository
{
    public async Task SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        await Set.Where(a => a.Featured && a.Id != id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Featured, valueExpression: false), ct);
        await Set.Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Featured, valueExpression: true), ct);
    }
}
