using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
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
    IFileService fileService,
    IAssignmentStatusTypeRepository statuses,
    IActivityRoleTypeRepository roleTypes,
    IActivityModalityTypeRepository modalityTypes,
    IUserRepository users,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IActivityService
{
    private static readonly SortMap<ActivityResponse> Sort = new SortMap<ActivityResponse>()
        .Add("activityStartsAt", a => a.ActivityStartsAt)
        .Add("activityEndsAt", a => a.ActivityEndsAt)
        .Add("title", a => a.Title)
        .Add("createdAt", a => a.CreatedAt)
        .Default("activityStartsAt")
        .Tie(a => a.Id);

    public Task<PagedResult<ActivityResponse>> ListAsync(
        ActivityListQuery query,
        CancellationToken ct = default
    )
    {
        var source = activities.Query().Select(Projections.Activity);

        if (query.EventId is { } eventId) source = source.Where(a => a.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(query.Title))
            source = source.Where(
                TextSearch.Contains<ActivityResponse>(
                    a => a.Title,
                    TextSearch.Normalize(query.Title)
                )
            );

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await executor.FirstOrDefaultAsync(
            activities.Query().Where(a => a.Id == id).Select(Projections.Activity),
            ct
        );
        if (response is null) return Error.NotFound(ErrorCode.ActivityNotFound);
        return response;
    }

    public async Task<IReadOnlyList<AssignedActivityResponse>> ListAssignedAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(assignment => assignment.UserId == userId)
                .Select(Projections.AssignedActivity)
                .OrderBy(assignment => assignment.ActivityStartsAt),
            ct
        );
    }

    public async Task<IReadOnlyList<ActivityRoleTypeResponse>> ListRoleTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            roleTypes.Query().OrderBy(role => role.Name).Select(Projections.ActivityRoleType),
            ct
        );
    }

    public async Task<IReadOnlyList<AssignmentStatusTypeResponse>> ListAssignmentStatusTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            statuses.Query().OrderBy(status => status.Name).Select(Projections.AssignmentStatusType),
            ct
        );
    }

    public async Task<IReadOnlyList<ActivityModalityTypeResponse>> ListModalityTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            modalityTypes
                .Query()
                .OrderBy(modality => modality.Name)
                .Select(Projections.ActivityModalityType),
            ct
        );
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

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ActivityThumbnailNotFound,
            ct
        );
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
            CreatedAt = clock.UtcNow,
            CreatedBy = userId,
        };
        ApplyAllowedRoles(activity, request.AllowedRoleTypes);

        await activities.AddAsync(activity, ct);
        await uow.SaveChangesAsync(ct);

        return await GetByIdAsync(activity.Id, ct);
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

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ActivityThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        if (!await modalityTypes.ExistsAsync(m => m.Id == request.ActivityModalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var allowedRoles = await EnsureAllowedRolesAsync(request.AllowedRoleTypes, ct);
        if (allowedRoles.IsFailure) return allowedRoles.Error!;

        var previousThumbnailId = activity.ThumbnailId;

        activity.Title = request.Title.Trim();
        activity.Description = request.Description;
        activity.Location = request.Location.Trim();
        activity.ActivityModalityTypeId = request.ActivityModalityTypeId;
        activity.ActivityStartsAt = schedule.Value.StartsAt;
        activity.ActivityEndsAt = schedule.Value.EndsAt;
        activity.ThumbnailId = request.ThumbnailId;
        activity.UpdatedAt = clock.UtcNow;
        activity.UpdatedBy = userId;

        activity.AllowedRoleTypes.Clear();
        ApplyAllowedRoles(activity, request.AllowedRoleTypes);

        await uow.SaveChangesAsync(ct);

        if (previousThumbnailId != request.ThumbnailId)
            await fileService.DeleteIfOrphanedAsync(previousThumbnailId, ct);

        return await GetByIdAsync(activityId, ct);
    }

    public async Task<Result> DeleteAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        activities.Remove(activity);
        await uow.SaveChangesAsync(ct);

        await fileService.DeleteIfOrphanedAsync(activity.ThumbnailId, ct);
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
        var signup = await EnsureSignupOpenAsync(activityId, isAdmin, ct);
        if (signup.IsFailure) return signup.Error!;

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

        var requestedStatus = await GetRequestedStatusAsync(ct);
        return new AssignmentResponse(
            userId,
            activityId,
            request.ActivityRoleTypeId,
            null,
            requestedStatus
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

        var signup = await EnsureSignupOpenAsync(activityId, isAdmin, ct);
        if (signup.IsFailure) return signup.Error!;

        var items = request.Assignments.DistinctBy(a => a.UserId).ToList();

        var memberIds = items
            .Select(item => item.UserId)
            .Where(id => id != actingUserId)
            .ToList();
        if (memberIds.Count > 0)
        {
            var householdIds = await executor.ToListAsync(
                users
                    .Query()
                    .Where(u => memberIds.Contains(u.Id) && u.ParentId == actingUserId)
                    .Select(u => u.Id),
                ct
            );
            if (memberIds.Except(householdIds).Any())
                return Error.Forbidden(ErrorCode.ActivityHouseholdMemberNotAllowed);
        }

        var allowedRoleIds = await executor.ToListAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .SelectMany(a => a.AllowedRoleTypes.Select(r => r.ActivityRoleTypeId)),
            ct
        );
        if (items.Exists(item => !allowedRoleIds.Contains(item.ActivityRoleTypeId)))
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);

        var userIds = items.Select(item => item.UserId).ToList();
        var alreadyAssigned = (
            await executor.ToListAsync(
                activities
                    .QueryAssignments()
                    .Where(x => x.ActivityId == activityId && userIds.Contains(x.UserId))
                    .Select(x => x.UserId),
                ct
            )
        ).ToHashSet();

        var requestedStatus = await GetRequestedStatusAsync(ct);
        var created = new List<AssignmentResponse>();
        foreach (var item in items)
        {
            if (alreadyAssigned.Contains(item.UserId)) continue;

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
                    requestedStatus
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
            var signup = await EnsureSignupOpenAsync(activityId, isAdmin, ct);
            if (signup.IsFailure) return signup.Error!;
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

        if (assignment.ActivityRoleTypeId == role.Id)
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

        // ActivityRoleTypeId is part of the composite primary key, which EF Core forbids mutating in
        // place; swap the role by removing the old assignment and inserting a new one that carries
        // the same status.
        var statusId = assignment.AssignmentStatusId;
        var statusName = assignment.AssignmentStatus?.Name ?? string.Empty;
        activities.RemoveAssignment(assignment);
        await activities.AddAssignmentAsync(
            new ActivityUserRoleAssignment
            {
                UserId = userId,
                ActivityId = activityId,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = statusId,
            },
            ct
        );
        await uow.SaveChangesAsync(ct);

        return new AssignmentResponse(
            userId,
            activityId,
            role.Id,
            role.Name,
            new AssignmentStatusResponse(statusId, statusName)
        );
    }

    public async Task<Result<TimeOverlapResponse>> VerifyTimeOverlapsAsync(
        Guid activityId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var target = await activities.FindAsync(a => a.Id == activityId, ct);
        if (target is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var assignments = await activities.GetUserAssignmentsAsync(userId, ct);
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
        await uow.SaveChangesAsync(ct);
        return roleType.ToResponse();
    }

    public async Task<Result> DeleteActivityRoleTypeAsync(Guid id, CancellationToken ct = default)
    {
        if (await roleTypes.RemoveAsync(x => x.Id == id, ct) == 0)
            return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureAllowedRolesAsync(
        IReadOnlyList<ActivityAllowedRoleRequest>? roles,
        CancellationToken ct
    )
    {
        if (roles is null) return Result.Success();

        var distinct = roles.Select(r => r.ActivityRoleTypeId).Distinct().ToList();
        if (distinct.Count == 0) return Result.Success();

        var existing = await roleTypes.CountAsync(r => distinct.Contains(r.Id), ct);
        if (existing != distinct.Count) return Error.BadRequest(ErrorCode.ActivityRoleTypeNotFound);

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

    private async Task<Result> EnsureSignupOpenAsync(
        Guid activityId,
        bool isAdmin,
        CancellationToken ct
    )
    {
        var window = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new SignupWindow(a.Event.SignupStartsAt, a.Event.SignupEndsAt)),
            ct
        );
        if (window is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var now = clock.UtcNow;
        if (!isAdmin && (now < window.StartsAt || now > window.EndsAt))
            return Error.BadRequest(ErrorCode.ActivitySignupClosed);

        return Result.Success();
    }

    private async Task<AssignmentStatusResponse> GetRequestedStatusAsync(CancellationToken ct)
    {
        var status = await statuses.FindAsync(
            s => s.Id == SeedIds.AssignmentStatusTypes.Requested,
            ct
        );
        return new AssignmentStatusResponse(
            SeedIds.AssignmentStatusTypes.Requested,
            status?.Name ?? string.Empty
        );
    }

    private Result<ActivitySchedule> ValidateActivitySchedule(
        Event ev,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt
    )
    {
        if (startsAt is not { } start || endsAt is not { } end)
            return Error.BadRequest(ErrorCode.ActivityScheduleRequired);

        if (end <= start) return Error.BadRequest(ErrorCode.ActivityScheduleInvalidRange);

        // Event bounds are calendar days in the app's timezone, so compare against the activity's
        // local day (not its UTC day, which drifts across midnight and rejects valid near-midnight
        // activities on the first/last event day).
        var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, clock.TimeZone).DateTime);
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, clock.TimeZone).DateTime);
        if (startDate < ev.EventStartsAt || endDate > ev.EventEndsAt)
            return Error.BadRequest(ErrorCode.ActivityScheduleOutsideEventRange);

        // Persist as UTC: Npgsql rejects a non-zero offset on a timestamptz column.
        return new ActivitySchedule(start.ToUniversalTime(), end.ToUniversalTime());
    }

    private readonly record struct ActivitySchedule(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

    private sealed record SignupWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
}