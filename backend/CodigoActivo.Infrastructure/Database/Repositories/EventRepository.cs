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
    public async Task<Event?> GetWithThumbnailAsync(Guid id, CancellationToken ct = default)
    {
        return await Set.AsNoTracking()
            .Include(e => e.Thumbnail)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Event>> ListAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    )
    {
        var query = Set.AsNoTracking().Include(e => e.Thumbnail).AsQueryable();

        if (startDate.HasValue)
        {
            var start = DateOnly.FromDateTime(startDate.Value.UtcDateTime);
            query = query.Where(e => e.EventEndsAt >= start);
        }

        if (endDate.HasValue)
        {
            var end = DateOnly.FromDateTime(endDate.Value.UtcDateTime);
            query = query.Where(e => e.EventStartsAt <= end);
        }

        return await query.OrderBy(e => e.EventStartsAt).ThenBy(e => e.Title).ToListAsync(ct);
    }

    public async Task<Event?> GetWithActivitiesAndAssignmentsAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        return await Set.AsNoTracking()
            .Include(e => e.Activities)
            .ThenInclude(a => a.Assignments)
            .Include(e => e.Activities)
            .ThenInclude(a => a.AllowedRoleTypes)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        await Set.Where(e => e.Featured && e.Id != id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Featured, false), ct);
        await Set.Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Featured, true), ct);
    }
}
