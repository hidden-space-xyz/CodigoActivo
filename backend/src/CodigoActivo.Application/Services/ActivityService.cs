using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

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
    IUnitOfWork uow,
    HybridCache cache,
    ICacheInvalidator cacheInvalidator,
    IEmailSender emailSender,
    ApplicationOptions application,
    ILogger<ActivityService> logger
) : IActivityService
{
    private const string EventPath = "/events";
    private const string AccountPath = "/account";

    private static readonly SortMap<ActivityResponse> Sort = new SortMap<ActivityResponse>()
        .Add("activityStartsAt", a => a.ActivityStartsAt)
        .Add("activityEndsAt", a => a.ActivityEndsAt)
        .Add("title", a => a.Title)
        .Add("modalityName", a => a.ModalityName)
        .Add("location", a => a.Location)
        .Add("createdAt", a => a.CreatedAt)
        .Default("activityStartsAt")
        .Tie(a => a.Id);

    public async Task<PagedResult<ActivityResponse>> ListAsync(
        ActivityListQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("activities:list", query),
            token => new ValueTask<PagedResult<ActivityResponse>>(FetchListAsync(query, token)),
            CachePolicies.PublicContent,
            [CacheTags.Activities],
            ct
        );
    }

    private Task<PagedResult<ActivityResponse>> FetchListAsync(
        ActivityListQuery query,
        CancellationToken ct
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

        source = source.WhereContains(a => a.Title, query.Title);
        source = source.WhereContains(a => a.Location, query.Location);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public Task<Result<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return cache.GetEntityAsync(
            executor,
            $"activities:id:{id}",
            () => activities.Query().Where(a => a.Id == id).Select(Projections.Activity),
            CacheTags.Activities,
            ErrorCode.ActivityNotFound,
            ct
        );
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

    public Task<IReadOnlyList<ActivityRoleTypeResponse>> ListRoleTypesAsync(
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:role-types",
            () => roleTypes.Query().OrderBy(role => role.Name).Select(Projections.ActivityRoleType),
            ct
        );
    }

    public Task<IReadOnlyList<AssignmentStatusTypeResponse>> ListAssignmentStatusTypesAsync(
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:assignment-status-types",
            () =>
                statuses
                    .Query()
                    .OrderBy(status => status.Name)
                    .Select(Projections.AssignmentStatusType),
            ct
        );
    }

    public Task<IReadOnlyList<ActivityModalityTypeResponse>> ListModalityTypesAsync(
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:modality-types",
            () =>
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
        var validated = await ValidateActivityAsync(
            eventId,
            request.ActivityStartsAt,
            request.ActivityEndsAt,
            request.ThumbnailId,
            request.ActivityModalityTypeId,
            request.RoleCapacities,
            ct
        );
        if (validated.IsFailure)
            return validated.Error!;

        var activity = new Activity
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Location = request.Location.Trim(),
            ActivityModalityTypeId = request.ActivityModalityTypeId,
            ActivityStartsAt = validated.Value.Schedule.StartsAt,
            ActivityEndsAt = validated.Value.Schedule.EndsAt,
            EventId = eventId,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = userId,
            RoleCapacities = validated
                .Value.Capacities.Select(item => new ActivityRoleCapacity
                {
                    ActivityRoleTypeId = item.RoleTypeId,
                    DesiredCount = item.DesiredCount,
                })
                .ToList(),
        };

        await activities.AddAsync(activity, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

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

        var validated = await ValidateActivityAsync(
            activity.EventId,
            request.ActivityStartsAt,
            request.ActivityEndsAt,
            request.ThumbnailId,
            request.ActivityModalityTypeId,
            request.RoleCapacities,
            ct
        );
        if (validated.IsFailure)
            return validated.Error!;

        var previousThumbnailId = activity.ThumbnailId;

        activity.Title = request.Title.Trim();
        activity.Description = request.Description;
        activity.Location = request.Location.Trim();
        activity.ActivityModalityTypeId = request.ActivityModalityTypeId;
        activity.ActivityStartsAt = validated.Value.Schedule.StartsAt;
        activity.ActivityEndsAt = validated.Value.Schedule.EndsAt;
        activity.ThumbnailId = request.ThumbnailId;
        activity.UpdatedAt = clock.UtcNow;
        activity.UpdatedBy = userId;

        SyncRoleCapacities(activity, validated.Value.Capacities);

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

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
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

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
        var signup = await EnsureSignupOpenAsync(activityId, [userId], isAdmin, ct);
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
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        await NotifySignupAsync(
            activityId,
            userId,
            [new SignupLine(userId, request.ActivityRoleTypeId)],
            ct
        );

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

        var signup = await EnsureSignupOpenAsync(activityId, [actingUserId], isAdmin, ct);
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

        var outsideHousehold = userIds.Exists(id =>
            id != actingUserId
            && (!memberById.TryGetValue(id, out var member) || member.ParentId != actingUserId)
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
        if (created.Count > 0)
        {
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
            await NotifySignupAsync(
                activityId,
                actingUserId,
                created.ConvertAll(item => new SignupLine(item.UserId, item.RoleTypeId)),
                ct
            );
        }

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
            var signup = await EnsureSignupOpenAsync(activityId, [userId], isAdmin, ct);
            if (signup.IsFailure)
                return signup.Error!;
        }

        activities.RemoveAssignment(assignment);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
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

        var previousStatusId = assignment.AssignmentStatusId;
        assignment.AssignmentStatusId = status.Id;
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        if (previousStatusId != status.Id && IsDecision(status.Id))
        {
            await NotifyDecisionAsync(
                activityId,
                userId,
                status.Id,
                assignment.ActivityRoleTypeId,
                ct
            );
        }

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

        var statusId = assignment.AssignmentStatusId;
        var statusName = assignment.AssignmentStatus?.Name ?? string.Empty;

        if (assignment.ActivityRoleTypeId != role.Id)
        {
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
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        }

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

        var roleNames = (await ListRoleTypesAsync(ct)).ToDictionary(r => r.Id, r => r.Name);

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

        if (requests.Select(item => item.ActivityRoleTypeId).ToHashSet().Count != requests.Count)
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

        var removed = activity.RoleCapacities.Where(capacity =>
            !desiredByRole.ContainsKey(capacity.ActivityRoleTypeId)
        );
        foreach (var existing in removed.ToList())
            activity.RoleCapacities.Remove(existing);

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
        IReadOnlyList<Guid> userIds,
        bool isAdmin,
        CancellationToken ct
    )
    {
        var window = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new SignupWindow(
                    a.Event.EarlySignupStartsAt,
                    a.Event.SignupStartsAt,
                    a.Event.SignupEndsAt
                )),
            ct
        );
        if (window is null)
            return Error.NotFound(ErrorCode.ActivityNotFound);

        if (isAdmin)
            return Result.Success();

        var now = clock.UtcNow;
        if (now > window.EndsAt)
            return Error.BadRequest(ErrorCode.ActivitySignupClosed);
        if (now >= window.StartsAt)
            return Result.Success();

        if (window.EarlyStartsAt is not { } earlyStart || now < earlyStart)
            return Error.BadRequest(ErrorCode.ActivitySignupClosed);

        return await AllAllowedInEarlySignupAsync(userIds, ct)
            ? Result.Success()
            : Error.BadRequest(ErrorCode.ActivitySignupEarlyOnly);
    }

    private async Task<bool> AllAllowedInEarlySignupAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var userTypeIds = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => u.Parent == null ? u.UserTypeId : u.Parent.UserTypeId),
            ct
        );
        return userTypeIds.All(IsEarlySignupUserType);
    }

    private static bool IsEarlySignupUserType(Guid userTypeId)
    {
        return userTypeId == SeedIds.UserTypes.Member || userTypeId == SeedIds.UserTypes.Sponsor;
    }

    private static bool IsDecision(Guid statusId)
    {
        return statusId == SeedIds.AssignmentStatusTypes.Confirmed
            || statusId == SeedIds.AssignmentStatusTypes.Denied;
    }

    private async Task NotifySignupAsync(
        Guid activityId,
        Guid recipientUserId,
        IReadOnlyList<SignupLine> lines,
        CancellationToken ct
    )
    {
        try
        {
            var details = await GetEmailDetailsAsync(activityId, ct);
            if (details is null)
                return;

            var contacts = await GetContactsAsync(
                lines.Select(line => line.UserId).Append(recipientUserId).Distinct().ToList(),
                ct
            );
            if (
                !contacts.TryGetValue(recipientUserId, out var target)
                || ResolveRecipient(target) is not { } recipient
            )
            {
                return;
            }

            var roleNames = await GetRoleNamesAsync(ct);
            var participants = new List<ActivitySignupParticipant>(lines.Count);
            foreach (var line in lines)
            {
                if (contacts.TryGetValue(line.UserId, out var contact))
                {
                    participants.Add(
                        new ActivitySignupParticipant(
                            contact.FullName,
                            roleNames.GetValueOrDefault(line.RoleTypeId, string.Empty)
                        )
                    );
                }
            }

            if (participants.Count == 0)
                return;

            await emailSender.SendAsync(
                ActivitySignupEmail.Create(
                    recipient.Address,
                    recipient.Name,
                    details,
                    participants,
                    clock.TimeZone,
                    BuildUrl(AccountPath)
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the signup confirmation email for activity {ActivityId}",
                activityId
            );
        }
    }

    private async Task NotifyDecisionAsync(
        Guid activityId,
        Guid userId,
        Guid statusId,
        Guid roleTypeId,
        CancellationToken ct
    )
    {
        try
        {
            var details = await GetEmailDetailsAsync(activityId, ct);
            if (details is null)
                return;

            var contacts = await GetContactsAsync([userId], ct);
            if (
                !contacts.TryGetValue(userId, out var contact)
                || ResolveRecipient(contact) is not { } recipient
            )
            {
                return;
            }

            var participantName = recipient.IsGuardian ? contact.FullName : null;
            var message =
                statusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ? ActivitySignupDecisionEmail.Confirmed(
                        recipient.Address,
                        recipient.Name,
                        participantName,
                        (await GetRoleNamesAsync(ct)).GetValueOrDefault(roleTypeId),
                        details,
                        clock.TimeZone
                    )
                    : ActivitySignupDecisionEmail.Denied(
                        recipient.Address,
                        recipient.Name,
                        participantName,
                        details,
                        clock.TimeZone
                    );

            await emailSender.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the signup decision email for activity {ActivityId}",
                activityId
            );
        }
    }

    private async Task<ActivityEmailDetails?> GetEmailDetailsAsync(
        Guid activityId,
        CancellationToken ct
    )
    {
        var data = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new ActivityEmailData(
                    a.Title,
                    a.Event.Title,
                    a.EventId,
                    a.Location,
                    a.ActivityStartsAt,
                    a.ActivityEndsAt
                )),
            ct
        );

        return data is null
            ? null
            : new ActivityEmailDetails(
                data.ActivityTitle,
                data.EventTitle,
                data.Location,
                data.StartsAt,
                data.EndsAt,
                BuildUrl($"{EventPath}/{data.EventId}")
            );
    }

    private async Task<Dictionary<Guid, UserContact>> GetContactsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var contacts = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new UserContact(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Parent == null ? null : u.Parent.FirstName,
                    u.Parent == null ? null : u.Parent.Email
                )),
            ct
        );

        return contacts.ToDictionary(contact => contact.Id);
    }

    private async Task<Dictionary<Guid, string>> GetRoleNamesAsync(CancellationToken ct)
    {
        var roles = await ListRoleTypesAsync(ct);
        return roles.ToDictionary(role => role.Id, role => role.Name);
    }

    private static NotificationRecipient? ResolveRecipient(UserContact contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Email))
            return new NotificationRecipient(contact.Email, contact.FirstName, IsGuardian: false);

        return string.IsNullOrWhiteSpace(contact.GuardianEmail)
            ? null
            : new NotificationRecipient(
                contact.GuardianEmail,
                contact.GuardianFirstName ?? string.Empty,
                IsGuardian: true
            );
    }

    private string BuildUrl(string path)
    {
        return $"{application.BaseUrl.TrimEnd('/')}{path}";
    }

    private async Task<AssignmentStatusResponse> GetRequestedStatusAsync(CancellationToken ct)
    {
        var status = (await ListAssignmentStatusTypesAsync(ct)).FirstOrDefault(s =>
            s.Id == SeedIds.AssignmentStatusTypes.Requested
        );
        return new AssignmentStatusResponse(
            SeedIds.AssignmentStatusTypes.Requested,
            status?.Name ?? string.Empty
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

    private async Task<Result<ValidatedActivity>> ValidateActivityAsync(
        Guid eventId,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        Guid thumbnailId,
        Guid modalityTypeId,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities,
        CancellationToken ct
    )
    {
        var eventDates = await GetEventDatesAsync(eventId, ct);
        if (eventDates is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var schedule = ValidateActivitySchedule(eventDates, startsAt, endsAt);
        if (schedule.IsFailure)
            return schedule.Error!;

        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
            return Error.BadRequest(ErrorCode.ActivityThumbnailNotFound);

        if (!await modalityTypes.ExistsAsync(m => m.Id == modalityTypeId, ct))
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);

        var capacities = await ValidateRoleCapacitiesAsync(roleCapacities, ct);
        if (capacities.IsFailure)
            return capacities.Error!;

        return new ValidatedActivity(schedule.Value, capacities.Value);
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

    private readonly record struct ValidatedActivity(
        ActivitySchedule Schedule,
        List<RoleCapacityItem> Capacities
    );

    private sealed record EventDates(DateOnly StartsAt, DateOnly EndsAt);

    private readonly record struct RoleCapacityItem(Guid RoleTypeId, int DesiredCount);

    private sealed record SignupWindow(
        DateTimeOffset? EarlyStartsAt,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    private readonly record struct SignupLine(Guid UserId, Guid RoleTypeId);

    private sealed record ActivityEmailData(
        string ActivityTitle,
        string EventTitle,
        Guid EventId,
        string Location,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    private sealed record UserContact(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? GuardianFirstName,
        string? GuardianEmail
    )
    {
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    private sealed record NotificationRecipient(string Address, string Name, bool IsGuardian);
}
