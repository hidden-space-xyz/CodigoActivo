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
    public async Task<Activity?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await Set.AsNoTracking()
            .Include(a => a.Thumbnail)
            .Include(a => a.AllowedRoleTypes)
                .ThenInclude(ar => ar.ActivityRoleType)
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<Activity?> GetWithAssignmentsAndUsersAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        return await Set.AsNoTracking()
            .Include(a => a.AllowedRoleTypes)
                .ThenInclude(ar => ar.ActivityRoleType)
            .Include(a => a.Assignments)
                .ThenInclude(asg => asg.User)
                    .ThenInclude(u => u.Parent)
            .Include(a => a.Assignments)
                .ThenInclude(asg => asg.ActivityRoleType)
            .Include(a => a.Assignments)
                .ThenInclude(asg => asg.AssignmentStatus)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<Activity?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        return await Set.Include(a => a.AllowedRoleTypes).FirstOrDefaultAsync(a => a.Id == id, ct);
    }

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

    public Task<bool> AllowedRoleExistsAsync(
        Guid activityId,
        Guid activityRoleTypeId,
        CancellationToken ct = default
    )
    {
        return Context.ActivityAllowedRoleTypes.AnyAsync(
            x => x.ActivityId == activityId && x.ActivityRoleTypeId == activityRoleTypeId,
            ct
        );
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
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    )
    {
        var query = Context
            .ActivityUserRoleAssignments.AsNoTracking()
            .Include(x => x.Activity)
            .Include(x => x.ActivityRoleType)
            .Include(x => x.AssignmentStatus)
            .Where(x => x.UserId == userId);

        if (startDate.HasValue)
        {
            query = query.Where(x => x.Activity.ActivityEndsAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.Activity.ActivityStartsAt <= endDate.Value);
        }

        return await query.OrderBy(x => x.Activity.ActivityStartsAt).ToListAsync(ct);
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
