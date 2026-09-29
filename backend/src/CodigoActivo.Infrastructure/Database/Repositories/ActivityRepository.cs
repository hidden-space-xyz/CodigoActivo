using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores and loads activities with their role capacities and signups.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class ActivityRepository(CodigoActivoDbContext context)
    : AggregateRepository<Activity>(context),
        IActivityRepository
{
    /// <inheritdoc />
    public Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Set.Include(activity => activity.RoleCapacities)
            .Include(activity => activity.Assignments)
            .FirstOrDefaultAsync(activity => activity.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            activity =>
                activity.EventId == eventId
                && (
                    activity.ActivityStartsAt < lowerInclusive
                    || activity.ActivityEndsAt >= upperExclusive
                ),
            ct
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListThumbnailIdsAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        return await Set.Where(activity => activity.EventId == eventId)
            .Select(activity => activity.ThumbnailId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> HasConfirmedAttendanceAsync(
        Guid eventId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        return (
            from activity in Set
            from assignment in activity.Assignments
            join user in Context.Users on assignment.UserId equals user.Id
            where
                activity.EventId == eventId
                && assignment.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                && (user.Id == userId || user.ParentId == userId)
            select assignment
        ).AnyAsync(ct);
    }
}
