using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves activity data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class ActivityRepository(CodigoActivoDbContext context)
    : Repository<Activity>(context),
        IActivityRepository
{
    /// <summary>
    /// Determines whether any outside range matches the condition.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="lowerInclusive">The lower inclusive value.</param>
    /// <param name="upperExclusive">The upper exclusive value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Finds a with role capacities that matches the supplied values.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity, or <see langword="null"/> when it is not found.</returns>
    public async Task<Activity?> FindWithRoleCapacitiesAsync(
        Guid activityId,
        CancellationToken ct = default
    )
    {
        return await Set.Include(a => a.RoleCapacities)
            .FirstOrDefaultAsync(a => a.Id == activityId, ct);
    }

    /// <summary>
    /// Determines whether an assignment already exists.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AssignmentExistsAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    )
    {
        return Context.ActivityUserRoleAssignments.AnyAsync(
            x => x.UserId == userId && x.ActivityId == activityId,
            ct
        );
    }

    /// <summary>
    /// Gets the requested assignment.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity user role assignment, or <see langword="null"/> when it is not found.</returns>
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

    /// <summary>
    /// Adds an assignment to the current unit of work.
    /// </summary>
    /// <param name="assignment">The assignment value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddAssignmentAsync(
        ActivityUserRoleAssignment assignment,
        CancellationToken ct = default
    )
    {
        await Context.ActivityUserRoleAssignments.AddAsync(assignment, ct);
    }

    /// <summary>
    /// Removes an assignment from persistent storage.
    /// </summary>
    /// <param name="assignment">The assignment value.</param>
    public void RemoveAssignment(ActivityUserRoleAssignment assignment)
    {
        Context.ActivityUserRoleAssignments.Remove(assignment);
    }

    /// <summary>
    /// Creates a query for activity assignments with their related user and role data.
    /// </summary>
    /// <returns>The resulting activity user role assignment value.</returns>
    public IQueryable<ActivityUserRoleAssignment> QueryAssignments()
    {
        return Context.ActivityUserRoleAssignments.AsNoTracking();
    }
}
