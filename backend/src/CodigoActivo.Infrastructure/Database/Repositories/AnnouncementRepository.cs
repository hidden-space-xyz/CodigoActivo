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
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        return SetExclusiveFeaturedAsync(Set, id, ct);
    }
}
