using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class ActivityService(
    IActivityRepository activities,
    IEventRepository events,
    IFileRepository files,
    IAssignmentStatusTypeRepository statuses,
    IActivityRoleTypeRepository roleTypes,
    IActivityModalityTypeRepository modalityTypes,
    IUserRepository users,
    IUnitOfWork uow
) : IActivityService
{
    public IQueryable<ActivityResponse> QueryActivities()
    {
        return activities.Query().Select(Projections.Activity);
    }

    public IQueryable<AssignedActivityResponse> QueryAssigned(Guid userId)
    {
        return activities
            .QueryAssignments()
            .Where(assignment => assignment.UserId == userId)
            .Select(Projections.AssignedActivity);
    }

    public IQueryable<ActivityRoleTypeResponse> QueryRoleTypes()
    {
        return roleTypes.Query().Select(Projections.ActivityRoleType);
    }

    public IQueryable<AssignmentStatusTypeResponse> QueryAssignmentStatusTypes()
    {
        return statuses.Query().Select(Projections.AssignmentStatusType);
    }

    public IQueryable<ActivityModalityTypeResponse> QueryModalityTypes()
    {
        return modalityTypes.Query().Select(Projections.ActivityModalityType);
    }

    public async Task<Result<ActivityResponse>> CreateAsync(
        Guid eventId,
        CreateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == eventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        var schedule = ValidateActivitySchedule(
            ev,
            request.ActivityStartsAt,
            request.ActivityEndsAt
        );
        if (schedule.IsFailure) return schedule.Error!;

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

        if (!await modalityTypes.ExistsAsync(m => m.Id == request.ActivityModalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var allowedRoles = await EnsureAllowedRolesAsync(request.AllowedRoleTypes, ct);
        if (allowedRoles.IsFailure) return allowedRoles.Error!;

        var activity = new Activity
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Location = request.Location.Trim(),
            ActivityModalityTypeId = request.ActivityModalityTypeId,
            ActivityStartsAt = schedule.Value.StartsAt,
            ActivityEndsAt = schedule.Value.EndsAt,
            EventId = eventId,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };
        ApplyAllowedRoles(activity, request.AllowedRoleTypes);

        await activities.AddAsync(activity, ct);
        await uow.SaveChangesAsync(ct);

        var created = await activities.GetByIdWithDetailsAsync(activity.Id, ct);
        return created!.ToResponse();
    }

    public async Task<Result<ActivityResponse>> UpdateAsync(
        Guid activityId,
        UpdateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetForEditAsync(activityId, ct);
        if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        var schedule = ValidateActivitySchedule(
            ev,
            request.ActivityStartsAt,
            request.ActivityEndsAt
        );
        if (schedule.IsFailure) return schedule.Error!;

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

        if (!await modalityTypes.ExistsAsync(m => m.Id == request.ActivityModalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var allowedRoles = await EnsureAllowedRolesAsync(request.AllowedRoleTypes, ct);
        if (allowedRoles.IsFailure) return allowedRoles.Error!;

        activity.Title = request.Title.Trim();
        activity.Description = request.Description;
        activity.Location = request.Location.Trim();
        activity.ActivityModalityTypeId = request.ActivityModalityTypeId;
        activity.ActivityStartsAt = schedule.Value.StartsAt;
        activity.ActivityEndsAt = schedule.Value.EndsAt;
        activity.ThumbnailId = request.ThumbnailId;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        activity.UpdatedBy = userId;

        activity.AllowedRoleTypes.Clear();
        ApplyAllowedRoles(activity, request.AllowedRoleTypes);

        await uow.SaveChangesAsync(ct);

        var updated = await activities.GetByIdWithDetailsAsync(activityId, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid activityId, CancellationToken ct = default)
    {
        if (!await activities.ExistsAsync(a => a.Id == activityId, ct))
            return Error.NotFound(ErrorCode.ActivityNotFound);

        await activities.RemoveAsync(a => a.Id == activityId, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        AssignRequest request,
        bool isAdmin,
        CancellationToken ct = default
    )
    {
        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        if (!isAdmin && !IsSignupOpen(ev, DateTimeOffset.UtcNow))
            return Error.BadRequest(ErrorCode.ActivitySignupClosed);

        if (!await activities.AllowedRoleExistsAsync(activityId, request.ActivityRoleTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);

        if (await activities.GetAssignmentAsync(userId, activityId, ct) is not null)
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);

        var assignment = new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = request.ActivityRoleTypeId,
            AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
        };
        await activities.AddAssignmentAsync(assignment, ct);
        await uow.SaveChangesAsync(ct);

        return new AssignmentResponse(
            userId,
            activityId,
            request.ActivityRoleTypeId,
            null,
            new AssignmentStatusResponse(
                SeedIds.AssignmentStatusTypes.Requested,
                nameof(SeedIds.AssignmentStatusTypes.Requested)
            )
        );
    }

    public async Task<Result<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        Guid actingUserId,
        AssignHouseholdRequest request,
        bool isAdmin,
        CancellationToken ct = default
    )
    {
        if (request.Assignments is null || request.Assignments.Count == 0)
            return Error.BadRequest(ErrorCode.ActivityHouseholdAssignmentsRequired);

        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        if (!isAdmin && !IsSignupOpen(ev, DateTimeOffset.UtcNow))
            return Error.BadRequest(ErrorCode.ActivitySignupClosed);

        var created = new List<AssignmentResponse>();
        foreach (var item in request.Assignments.DistinctBy(a => a.UserId))
        {
            if (item.UserId != actingUserId)
            {
                var target = await users.FindAsync(u => u.Id == item.UserId, ct);
                if (target is null || target.ParentId != actingUserId)
                    return Error.Forbidden(ErrorCode.ActivityHouseholdMemberNotAllowed);
            }

            if (!await activities.AllowedRoleExistsAsync(activityId, item.ActivityRoleTypeId, ct))
                return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);

            if (await activities.GetAssignmentAsync(item.UserId, activityId, ct) is not null) continue;

            await activities.AddAssignmentAsync(
                new ActivityUserRoleAssignment
                {
                    UserId = item.UserId,
                    ActivityId = activityId,
                    ActivityRoleTypeId = item.ActivityRoleTypeId,
                    AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
                },
                ct
            );
            created.Add(
                new AssignmentResponse(
                    item.UserId,
                    activityId,
                    item.ActivityRoleTypeId,
                    null,
                    new AssignmentStatusResponse(
                        SeedIds.AssignmentStatusTypes.Requested,
                        nameof(SeedIds.AssignmentStatusTypes.Requested)
                    )
                )
            );
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<AssignmentResponse>>(created);
    }

    public async Task<Result> UnassignAsync(
        Guid activityId,
        Guid userId,
        bool isAdmin,
        CancellationToken ct = default
    )
    {
        var assignment = await activities.GetAssignmentAsync(userId, activityId, ct);
        if (assignment is null) return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        if (!isAdmin)
        {
            var activity = await activities.FindAsync(a => a.Id == activityId, ct);
            if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

            var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
            if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

            if (!IsSignupOpen(ev, DateTimeOffset.UtcNow)) return Error.BadRequest(ErrorCode.ActivitySignupClosed);
        }

        activities.RemoveAssignment(assignment);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AssignmentResponse>> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentStatusRequest request,
        CancellationToken ct = default
    )
    {
        var assignment = await activities.GetAssignmentAsync(userId, activityId, ct);
        if (assignment is null) return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        var status = await statuses.FindAsync(s => s.Id == request.AssignmentStatusId, ct);
        if (status is null) return Error.NotFound(ErrorCode.AssignmentStatusTypeNotFound);

        assignment.AssignmentStatusId = status.Id;
        await uow.SaveChangesAsync(ct);

        return new AssignmentResponse(
            userId,
            activityId,
            assignment.ActivityRoleTypeId,
            assignment.ActivityRoleType?.Name,
            new AssignmentStatusResponse(status.Id, status.Name)
        );
    }

    public async Task<Result<AssignmentResponse>> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentRoleRequest request,
        CancellationToken ct = default
    )
    {
        var assignment = await activities.GetAssignmentAsync(userId, activityId, ct);
        if (assignment is null) return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        if (!await activities.AllowedRoleExistsAsync(activityId, request.ActivityRoleTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);

        var role = await roleTypes.FindAsync(r => r.Id == request.ActivityRoleTypeId, ct);
        if (role is null) return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);

        assignment.ActivityRoleTypeId = role.Id;
        await uow.SaveChangesAsync(ct);

        return new AssignmentResponse(
            userId,
            activityId,
            role.Id,
            role.Name,
            new AssignmentStatusResponse(
                assignment.AssignmentStatusId,
                assignment.AssignmentStatus?.Name ?? string.Empty
            )
        );
    }

    public async Task<Result<TimeOverlapResponse>> VerifyTimeOverlapsAsync(
        Guid activityId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (!await activities.ExistsAsync(a => a.Id == activityId, ct))
            return Error.NotFound(ErrorCode.ActivityNotFound);

        var target = await activities.FindAsync(a => a.Id == activityId, ct);
        if (target is null) return new TimeOverlapResponse(false, []);

        var assignments = await activities.GetUserAssignmentsAsync(userId, null, null, ct);
        var overlaps = assignments
            .Where(x => x.ActivityId != activityId && Overlaps(target, x.Activity))
            .Select(x => new OverlappingActivityResponse(
                x.ActivityId,
                x.Activity.Title,
                x.Activity.ActivityStartsAt,
                x.Activity.ActivityEndsAt
            ))
            .ToList();

        return new TimeOverlapResponse(overlaps.Count > 0, overlaps);
    }

    public async Task<
        IReadOnlyList<HouseholdMemberAssignmentResponse>
    > GetHouseholdAssignmentsAsync(Guid actingUserId, Guid eventId, CancellationToken ct = default)
    {
        var children = await users.ListChildrenWithDetailsAsync(actingUserId, ct);
        var ids = new List<Guid> { actingUserId };
        ids.AddRange(children.Select(child => child.Id));

        var assignments = await activities.GetAssignmentsForUsersByEventAsync(ids, eventId, ct);
        return assignments
            .Select(x => new HouseholdMemberAssignmentResponse(
                x.ActivityId,
                x.UserId,
                x.User.FirstName,
                x.User.LastName,
                x.ActivityRoleTypeId,
                x.ActivityRoleType?.Name ?? string.Empty,
                x.AssignmentStatusId,
                x.AssignmentStatus?.Name ?? string.Empty
            ))
            .ToList();
    }

    public async Task<Result<ActivityRoleTypeResponse>> CreateActivityRoleTypeAsync(
        CreateActivityRoleTypeRequest request,
        CancellationToken ct = default
    )
    {
        var name = request.Name.Trim();
        if (await roleTypes.ExistsAsync(x => x.Name == name, ct))
            return Error.Conflict(ErrorCode.ActivityRoleTypeNameAlreadyExists);

        var roleType = new ActivityRoleType
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
        };
        await roleTypes.AddAsync(roleType, ct);
        await uow.SaveChangesAsync(ct);
        return roleType.ToResponse();
    }

    public async Task<Result<ActivityRoleTypeResponse>> UpdateActivityRoleTypeAsync(
        Guid id,
        UpdateActivityRoleTypeRequest request,
        CancellationToken ct = default
    )
    {
        var roleType = await roleTypes.FindAsync(x => x.Id == id, ct);
        if (roleType is null) return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);

        var name = request.Name.Trim();
        if (await roleTypes.ExistsAsync(x => x.Name == name && x.Id != id, ct))
            return Error.Conflict(ErrorCode.ActivityRoleTypeNameAlreadyExists);

        roleType.Name = name;
        roleType.Description = request.Description?.Trim() ?? string.Empty;
        roleTypes.Update(roleType);
        await uow.SaveChangesAsync(ct);
        return roleType.ToResponse();
    }

    public async Task<Result> DeleteActivityRoleTypeAsync(Guid id, CancellationToken ct = default)
    {
        if (!await roleTypes.ExistsAsync(x => x.Id == id, ct))
            return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);

        await roleTypes.RemoveAsync(x => x.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureAllowedRolesAsync(
        IReadOnlyList<ActivityAllowedRoleRequest>? roles,
        CancellationToken ct
    )
    {
        if (roles is null) return Result.Success();

        foreach (var roleId in roles.Select(r => r.ActivityRoleTypeId).Distinct())
        {
            if (!await roleTypes.ExistsAsync(r => r.Id == roleId, ct))
                return Error.BadRequest(ErrorCode.ActivityRoleTypeNotFound);
        }

        return Result.Success();
    }

    private static void ApplyAllowedRoles(
        Activity activity,
        IReadOnlyList<ActivityAllowedRoleRequest>? roles
    )
    {
        if (roles is null) return;

        foreach (var role in roles.DistinctBy(r => r.ActivityRoleTypeId))
            activity.AllowedRoleTypes.Add(
                new ActivityAllowedRoleType
                {
                    ActivityId = activity.Id,
                    ActivityRoleTypeId = role.ActivityRoleTypeId,
                }
            );
    }

    private static bool Overlaps(Activity a, Activity b)
    {
        return a.ActivityStartsAt < b.ActivityEndsAt && b.ActivityStartsAt < a.ActivityEndsAt;
    }

    private static bool IsSignupOpen(Event ev, DateTimeOffset now)
    {
        return now >= ev.SignupStartsAt && now <= ev.SignupEndsAt;
    }

    private static Result<ActivitySchedule> ValidateActivitySchedule(
        Event ev,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt
    )
    {
        if (startsAt is not { } start || endsAt is not { } end)
            return Error.BadRequest(ErrorCode.ActivityScheduleRequired);

        if (end <= start) return Error.BadRequest(ErrorCode.ActivityScheduleInvalidRange);

        var startDate = DateOnly.FromDateTime(start.UtcDateTime);
        var endDate = DateOnly.FromDateTime(end.UtcDateTime);
        if (startDate < ev.EventStartsAt || endDate > ev.EventEndsAt)
            return Error.BadRequest(ErrorCode.ActivityScheduleOutsideEventRange);

        return new ActivitySchedule(start, end);
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
            return Error.BadRequest(ErrorCode.ActivityThumbnailNotFound);

        return Result.Success();
    }

    private readonly record struct ActivitySchedule(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
}