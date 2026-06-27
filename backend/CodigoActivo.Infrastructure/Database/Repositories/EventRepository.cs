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
            query = query.Where(e => e.EventEndsAt == null || e.EventEndsAt >= startDate);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.EventStartsAt == null || e.EventStartsAt <= endDate);
        }

        return await query
            .OrderBy(e => e.EventStartsAt ?? DateTimeOffset.MaxValue)
            .ThenBy(e => e.Title)
            .ToListAsync(ct);
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
}
