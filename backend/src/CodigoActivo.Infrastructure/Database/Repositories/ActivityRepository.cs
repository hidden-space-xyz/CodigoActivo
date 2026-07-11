using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class ActivityRepository(CodigoActivoDbContext context)
    : Repository<Activity>(context),
        IActivityRepository
{
    public Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            a =>
                a.EventId == eventId
                && (a.ActivityStartsAt < lowerInclusive || a.ActivityEndsAt >= upperExclusive),
            ct
        );
    }

    public async Task<Activity?> FindWithRoleCapacitiesAsync(
        Guid activityId,
        CancellationToken ct = default
    )
    {
        return await Set.Include(a => a.RoleCapacities)
            .FirstOrDefaultAsync(a => a.Id == activityId, ct);
    }

    public async Task<ActivityUserRoleAssignment?> GetAssignmentAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    )
    {
        return await Context
            .ActivityUserRoleAssignments.Include(x => x.AssignmentStatus)
            .Include(x => x.ActivityRoleType)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ActivityId == activityId, ct);
    }

    public async Task AddAssignmentAsync(
        ActivityUserRoleAssignment assignment,
        CancellationToken ct = default
    )
    {
        await Context.ActivityUserRoleAssignments.AddAsync(assignment, ct);
    }

    public void RemoveAssignment(ActivityUserRoleAssignment assignment)
    {
        Context.ActivityUserRoleAssignments.Remove(assignment);
    }

    public async Task<IReadOnlyList<ActivityUserRoleAssignment>> GetUserAssignmentsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await Context
            .ActivityUserRoleAssignments.AsNoTracking()
            .Include(x => x.Activity)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Activity.ActivityStartsAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ActivityUserRoleAssignment>> GetAssignmentsForUsersByEventAsync(
        IReadOnlyList<Guid> userIds,
        Guid eventId,
        CancellationToken ct = default
    )
    {
        return await Context
            .ActivityUserRoleAssignments.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.ActivityRoleType)
            .Include(x => x.AssignmentStatus)
            .Where(x => x.Activity.EventId == eventId && userIds.Contains(x.UserId))
            .ToListAsync(ct);
    }

    public IQueryable<ActivityUserRoleAssignment> QueryAssignments()
    {
        return Context.ActivityUserRoleAssignments.AsNoTracking();
    }
}
