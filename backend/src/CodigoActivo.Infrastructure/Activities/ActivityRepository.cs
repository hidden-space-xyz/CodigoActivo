using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Stores and loads activities with their role capacities and signups.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class ActivityRepository(CodigoActivoDbContext context)
    : AggregateRepository<Activity>(context),
        IActivityRepository
{
    /// <inheritdoc />
    public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default)
    {
        return Set.Include(activity => activity.RoleCapacities)
            .Include(activity => activity.Assignments)
            .FirstOrDefaultAsync(activity => activity.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<bool> AnyOutsideRangeAsync(
        EventId eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            activity =>
                activity.EventId == eventId
                && (
                    EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityStartsAt)
                        < lowerInclusive
                    || EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityEndsAt)
                        >= upperExclusive
                ),
            ct
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredFileId>> ListThumbnailIdsAsync(
        EventId eventId,
        CancellationToken ct = default
    )
    {
        return await Set.Where(activity => activity.EventId == eventId)
            .Select(activity => activity.ThumbnailId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> HasConfirmedAttendanceAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    )
    {
        return (
            from activity in Set
            from assignment in activity.Assignments
            join user in Context.Users on assignment.UserId equals user.Id
            where
                activity.EventId == eventId
                && assignment.Status == AssignmentStatus.Confirmed
                && (user.Id == userId || user.ParentId == userId)
            select assignment
        ).AnyAsync(ct);
    }
}
