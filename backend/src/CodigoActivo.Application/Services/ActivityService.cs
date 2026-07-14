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
        .Add("modalityName", a => a.ModalityName)
        .Add("location", a => a.Location)
        .Add("createdAt", a => a.CreatedAt)
        .Default("activityStartsAt")
        .Tie(a => a.Id);

    public Task<PagedResult<ActivityResponse>> ListAsync(
        ActivityListQuery query,
        CancellationToken ct = default
    )
    {
        var source = activities.Query().Select(Projections.Activity);

        if (query.EventId is { } eventId)
            source = source.Where(a => a.EventId == eventId);
        if (query.ModalityTypeId is { } modalityTypeId)
            source = source.Where(a => a.ModalityId == modalityTypeId);
        if (query.ActivityDateFrom is { } activityDateFrom)
        {
            var activityLower = LocalDayRange.LowerUtc(activityDateFrom, clock.TimeZone);
            source = source.Where(a => a.ActivityEndsAt >= activityLower);
        }

        if (query.ActivityDateTo is { } activityDateTo)
        {
            var activityUpper = LocalDayRange.UpperExclusiveUtc(activityDateTo, clock.TimeZone);
            source = source.Where(a => a.ActivityStartsAt < activityUpper);
        }

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            source = source.Where(
                TextSearch.Contains<ActivityResponse>(
                    a => a.Title,
                    TextSearch.Normalize(query.Title)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            source = source.Where(
                TextSearch.Contains<ActivityResponse>(
                    a => a.Location,
                    TextSearch.Normalize(query.Location)
                )
            );
        }

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<ActivityResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            activities.Query().Where(a => a.Id == id).Select(Projections.Activity),
            ct
        );
        return response is null
            ? (Result<ActivityResponse>)Error.NotFound(ErrorCode.ActivityNotFound)
            : (Result<ActivityResponse>)response;
    }

    public async Task<IReadOnlyList<AssignedActivityResponse>> ListAssignedAsync(
        Guid userId,
        Guid? eventId = null,
        CancellationToken ct = default
    )
    {
        var source = activities
            .QueryAssignments()
            .Where(assignment => assignment.UserId == userId)
            .Select(Projections.AssignedActivity);

        if (eventId is { } filterEventId)
            source = source.Where(assignment => assignment.EventId == filterEventId);

        return await executor.ToListAsync(
            source.OrderBy(assignment => assignment.ActivityStartsAt),
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
            statuses
                .Query()
                .OrderBy(status => status.Name)
                .Select(Projections.AssignmentStatusType),
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
        var eventDates = await GetEventDatesAsync(eventId, ct);
        if (eventDates is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var schedule = ValidateActivitySchedule(
            eventDates,
            request.ActivityStartsAt,
            request.ActivityEndsAt
        );
        if (schedule.IsFailure)
            return schedule.Error!;

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ActivityThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

        if (!await modalityTypes.ExistsAsync(m => m.Id == request.ActivityModalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var capacities = await ValidateRoleCapacitiesAsync(request.RoleCapacities, ct);
        if (capacities.IsFailure)
            return capacities.Error!;

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
            RoleCapacities = capacities
                .Value.Select(item => new ActivityRoleCapacity
                {
                    ActivityRoleTypeId = item.RoleTypeId,
                    DesiredCount = item.DesiredCount,
                })
                .ToList(),
        };

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
        var activity = await activities.FindWithRoleCapacitiesAsync(activityId, ct);
        if (activity is null)
            return Error.NotFound(ErrorCode.ActivityNotFound);

        var eventDates = await GetEventDatesAsync(activity.EventId, ct);
        if (eventDates is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var schedule = ValidateActivitySchedule(
            eventDates,
            request.ActivityStartsAt,
            request.ActivityEndsAt
        );
        if (schedule.IsFailure)
            return schedule.Error!;

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ActivityThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

        if (!await modalityTypes.ExistsAsync(m => m.Id == request.ActivityModalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var capacities = await ValidateRoleCapacitiesAsync(request.RoleCapacities, ct);
        if (capacities.IsFailure)
            return capacities.Error!;

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

        SyncRoleCapacities(activity, capacities.Value);

        await uow.SaveChangesAsync(ct);

        if (previousThumbnailId != request.ThumbnailId)
            await fileService.DeleteIfOrphanedAsync(previousThumbnailId, ct);

        return await GetByIdAsync(activityId, ct);
    }

    public async Task<Result> DeleteAsync(Guid activityId, CancellationToken ct = default)
    {
        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null)
            return Error.NotFound(ErrorCode.ActivityNotFound);

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
        if (signup.IsFailure)
            return signup.Error!;

        var userTypeId = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == userId).Select(u => (Guid?)u.UserTypeId),
            ct
        );
        if (userTypeId is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (!IsSignupRoleAllowed(userTypeId.Value, request.ActivityRoleTypeId))
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);

        if (await activities.AssignmentExistsAsync(userId, activityId, ct))
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);

        var assignment = new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = request.ActivityRoleTypeId,
            AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
            CreatedAt = clock.UtcNow,
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
        if (signup.IsFailure)
            return signup.Error!;

        var items = request.Assignments.DistinctBy(a => a.UserId).ToList();
        var userIds = items.ConvertAll(item => item.UserId);

        var members = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.UserTypeId,
                    u.ParentId,
                }),
            ct
        );
        var memberById = members.ToDictionary(u => u.Id);

        var outsideHousehold = userIds
            .Where(id => id != actingUserId)
            .Any(id =>
                !memberById.TryGetValue(id, out var member) || member.ParentId != actingUserId
            );
        if (outsideHousehold)
            return Error.Forbidden(ErrorCode.ActivityHouseholdMemberNotAllowed);

        if (
            items.Exists(item =>
                !memberById.TryGetValue(item.UserId, out var member)
                || !IsSignupRoleAllowed(member.UserTypeId, item.ActivityRoleTypeId)
            )
        )
        {
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);
        }

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
            if (alreadyAssigned.Contains(item.UserId))
                continue;

            await activities.AddAssignmentAsync(
                new ActivityUserRoleAssignment
                {
                    UserId = item.UserId,
                    ActivityId = activityId,
                    ActivityRoleTypeId = item.ActivityRoleTypeId,
                    AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
                    CreatedAt = clock.UtcNow,
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
        if (assignment is null)
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        if (!isAdmin)
        {
            var signup = await EnsureSignupOpenAsync(activityId, isAdmin, ct);
            if (signup.IsFailure)
                return signup.Error!;
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
        if (assignment is null)
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        var status = await statuses.FindAsync(s => s.Id == request.AssignmentStatusId, ct);
        if (status is null)
            return Error.NotFound(ErrorCode.AssignmentStatusTypeNotFound);

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
        if (assignment is null)
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);

        var role = await roleTypes.FindAsync(r => r.Id == request.ActivityRoleTypeId, ct);
        if (role is null)
            return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);

        if (assignment.ActivityRoleTypeId == role.Id)
        {
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
                CreatedAt = assignment.CreatedAt,
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
        var target = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new { a.ActivityStartsAt, a.ActivityEndsAt }),
            ct
        );
        if (target is null)
            return Error.NotFound(ErrorCode.ActivityNotFound);

        var overlaps = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(x =>
                    x.UserId == userId
                    && x.ActivityId != activityId
                    && x.Activity.ActivityStartsAt < target.ActivityEndsAt
                    && target.ActivityStartsAt < x.Activity.ActivityEndsAt
                )
                .OrderBy(x => x.Activity.ActivityStartsAt)
                .ThenBy(x => x.ActivityId)
                .Select(x => new OverlappingActivityResponse(
                    x.ActivityId,
                    x.Activity.Title,
                    x.Activity.ActivityStartsAt,
                    x.Activity.ActivityEndsAt
                )),
            ct
        );

        return new TimeOverlapResponse(overlaps.Count > 0, overlaps);
    }

    public async Task<
        IReadOnlyList<HouseholdMemberAssignmentResponse>
    > GetHouseholdAssignmentsAsync(Guid actingUserId, Guid eventId, CancellationToken ct = default)
    {
        return await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(x =>
                    x.Activity.EventId == eventId
                    && (x.UserId == actingUserId || x.User.ParentId == actingUserId)
                )
                .OrderBy(x => x.User.FirstName)
                .ThenBy(x => x.User.LastName)
                .ThenBy(x => x.Activity.ActivityStartsAt)
                .ThenBy(x => x.ActivityId)
                .Select(x => new HouseholdMemberAssignmentResponse(
                    x.ActivityId,
                    x.UserId,
                    x.User.FirstName,
                    x.User.LastName,
                    x.ActivityRoleTypeId,
                    x.ActivityRoleType.Name,
                    x.AssignmentStatusId,
                    x.AssignmentStatus.Name
                )),
            ct
        );
    }

    public async Task<IReadOnlyList<HouseholdSignupRolesResponse>> GetHouseholdSignupRolesAsync(
        Guid actingUserId,
        CancellationToken ct = default
    )
    {
        var members = await executor.ToListAsync(
            users
                .Query()
                .Where(u => u.Id == actingUserId || u.ParentId == actingUserId)
                .Select(u => new { u.Id, u.UserTypeId }),
            ct
        );

        var roleNames = (await roleTypes.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.Name);

        return members
            .Select(member => new HouseholdSignupRolesResponse(
                member.Id,
                SignupRoleIdsFor(member.UserTypeId)
                    .Select(roleId => new SignupRoleResponse(
                        roleId,
                        roleNames.GetValueOrDefault(roleId, string.Empty)
                    ))
                    .ToList()
            ))
            .ToList();
    }

    private async Task<Result<List<RoleCapacityItem>>> ValidateRoleCapacitiesAsync(
        IReadOnlyList<ActivityRoleCapacityRequest>? requests,
        CancellationToken ct
    )
    {
        if (requests is null || requests.Count == 0)
            return new List<RoleCapacityItem>();

        if (requests.DistinctBy(item => item.ActivityRoleTypeId).Count() != requests.Count)
            return Error.BadRequest(ErrorCode.ActivityRoleCapacityDuplicated);

        var roleIds = requests.Select(item => item.ActivityRoleTypeId).ToList();
        var knownCount = await roleTypes.CountAsync(role => roleIds.Contains(role.Id), ct);
        if (knownCount != roleIds.Count)
            return Error.BadRequest(ErrorCode.ActivityRoleTypeNotFound);

        return requests
            .Select(item => new RoleCapacityItem(item.ActivityRoleTypeId, item.DesiredCount!.Value))
            .ToList();
    }

    private static void SyncRoleCapacities(Activity activity, List<RoleCapacityItem> desired)
    {
        var desiredByRole = desired.ToDictionary(
            item => item.RoleTypeId,
            item => item.DesiredCount
        );

        foreach (var existing in activity.RoleCapacities.ToList())
        {
            if (!desiredByRole.ContainsKey(existing.ActivityRoleTypeId))
                activity.RoleCapacities.Remove(existing);
        }

        foreach (var (roleTypeId, desiredCount) in desiredByRole)
        {
            var existing = activity.RoleCapacities.FirstOrDefault(capacity =>
                capacity.ActivityRoleTypeId == roleTypeId
            );
            if (existing is null)
            {
                activity.RoleCapacities.Add(
                    new ActivityRoleCapacity
                    {
                        ActivityId = activity.Id,
                        ActivityRoleTypeId = roleTypeId,
                        DesiredCount = desiredCount,
                    }
                );
            }
            else
            {
                existing.DesiredCount = desiredCount;
            }
        }
    }

    private static IEnumerable<Guid> SignupRoleIdsFor(Guid userTypeId)
    {
        yield return SeedIds.ActivityRoleTypes.Participant;
        yield return SeedIds.ActivityRoleTypes.Volunteer;
        if (userTypeId == SeedIds.UserTypes.Member)
            yield return SeedIds.ActivityRoleTypes.Leader;
    }

    private static bool IsSignupRoleAllowed(Guid userTypeId, Guid roleTypeId)
    {
        return SignupRoleIdsFor(userTypeId).Contains(roleTypeId);
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
        if (window is null)
            return Error.NotFound(ErrorCode.ActivityNotFound);

        var now = clock.UtcNow;
        return !isAdmin && (now < window.StartsAt || now > window.EndsAt)
            ? (Result)Error.BadRequest(ErrorCode.ActivitySignupClosed)
            : Result.Success();
    }

    private async Task<AssignmentStatusResponse> GetRequestedStatusAsync(CancellationToken ct)
    {
        var name = await executor.FirstOrDefaultAsync(
            statuses
                .Query()
                .Where(s => s.Id == SeedIds.AssignmentStatusTypes.Requested)
                .Select(s => s.Name),
            ct
        );
        return new AssignmentStatusResponse(
            SeedIds.AssignmentStatusTypes.Requested,
            name ?? string.Empty
        );
    }

    private Task<EventDates?> GetEventDatesAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events
                .Query()
                .Where(e => e.Id == eventId)
                .Select(e => new EventDates(e.EventStartsAt, e.EventEndsAt)),
            ct
        );
    }

    private Result<ActivitySchedule> ValidateActivitySchedule(
        EventDates eventDates,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt
    )
    {
        if (startsAt is not { } start || endsAt is not { } end)
            return Error.BadRequest(ErrorCode.ActivityScheduleRequired);

        if (end <= start)
            return Error.BadRequest(ErrorCode.ActivityScheduleInvalidRange);

        var startDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(start, clock.TimeZone).DateTime
        );
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, clock.TimeZone).DateTime);
        return startDate < eventDates.StartsAt || endDate > eventDates.EndsAt
            ? (Result<ActivitySchedule>)
                Error.BadRequest(ErrorCode.ActivityScheduleOutsideEventRange)
            : (Result<ActivitySchedule>)
                new ActivitySchedule(start.ToUniversalTime(), end.ToUniversalTime());
    }

    private readonly record struct ActivitySchedule(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

    private sealed record EventDates(DateOnly StartsAt, DateOnly EndsAt);

    private readonly record struct RoleCapacityItem(Guid RoleTypeId, int DesiredCount);

    private sealed record SignupWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
}
