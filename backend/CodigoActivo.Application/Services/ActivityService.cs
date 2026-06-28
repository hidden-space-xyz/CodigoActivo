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
    IUserRepository users,
    IUnitOfWork uow
) : IActivityService
{
    public async Task<IReadOnlyList<ActivityResponse>> ListByEventAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var list = await activities.ListByEventAsync(eventId, ct);
        return list.Select(a => a.ToResponse()).ToList();
    }

    public async Task<Result<ActivityResponse>> GetByIdAsync(
        Guid eventId,
        Guid activityId,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetByIdWithDetailsAsync(activityId, ct);
        var response = activity?.ToResponse();

        if (response is null || response.EventId != eventId)
        {
            return Error.NotFound();
        }

        return response;
    }

    public async Task<Result<ActivityResponse>> CreateAsync(
        Guid eventId,
        CreateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == eventId, ct))
        {
            return Error.NotFound();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        var activity = new Activity
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            ActivityStartsAt = request.ActivityStartsAt,
            ActivityEndsAt = request.ActivityEndsAt,
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
        if (activity is null)
        {
            return Error.NotFound();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        activity.Title = request.Title.Trim();
        activity.Description = request.Description;
        activity.ActivityStartsAt = request.ActivityStartsAt;
        activity.ActivityEndsAt = request.ActivityEndsAt;
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
        {
            return Error.NotFound();
        }

        await activities.RemoveAsync(a => a.Id == activityId, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        AssignRequest request,
        CancellationToken ct = default
    )
    {
        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null)
        {
            return Error.NotFound();
        }

        var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        if (
            (ev.SignupStartsAt is { } signupStart && now < signupStart)
            || (ev.SignupEndsAt is { } signupEnd && now > signupEnd)
        )
        {
            return Error.Validation();
        }

        if (!await activities.AllowedRoleExistsAsync(activityId, request.ActivityRoleTypeId, ct))
        {
            return Error.Validation();
        }

        if (await activities.GetAssignmentAsync(userId, activityId, ct) is not null)
        {
            return Error.Validation();
        }

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
        CancellationToken ct = default
    )
    {
        if (request.Assignments is null || request.Assignments.Count == 0)
        {
            return Error.Validation();
        }

        var activity = await activities.FindAsync(a => a.Id == activityId, ct);
        if (activity is null)
        {
            return Error.NotFound();
        }

        var ev = await events.FindAsync(e => e.Id == activity.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        if (
            (ev.SignupStartsAt is { } signupStart && now < signupStart)
            || (ev.SignupEndsAt is { } signupEnd && now > signupEnd)
        )
        {
            return Error.Validation();
        }

        var created = new List<AssignmentResponse>();
        foreach (var item in request.Assignments.DistinctBy(a => a.UserId))
        {
            if (item.UserId != actingUserId)
            {
                var target = await users.FindAsync(u => u.Id == item.UserId, ct);
                if (target is null || target.ParentId != actingUserId)
                {
                    return Error.Forbidden();
                }
            }

            if (!await activities.AllowedRoleExistsAsync(activityId, item.ActivityRoleTypeId, ct))
            {
                return Error.Validation();
            }

            if (await activities.GetAssignmentAsync(item.UserId, activityId, ct) is not null)
            {
                continue;
            }

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
        CancellationToken ct = default
    )
    {
        var assignment = await activities.GetAssignmentAsync(userId, activityId, ct);
        if (assignment is null)
        {
            return Error.NotFound();
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
        {
            return Error.NotFound();
        }

        var status = await statuses.FindAsync(s => s.Id == request.AssignmentStatusId, ct);
        if (status is null)
        {
            return Error.NotFound();
        }

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
        {
            return Error.NotFound();
        }

        if (!await activities.AllowedRoleExistsAsync(activityId, request.ActivityRoleTypeId, ct))
        {
            return Error.Validation();
        }

        var role = await roleTypes.FindAsync(r => r.Id == request.ActivityRoleTypeId, ct);
        if (role is null)
        {
            return Error.NotFound();
        }

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
        {
            return Error.NotFound();
        }

        var target = await activities.FindAsync(a => a.Id == activityId, ct);
        if (target is null)
        {
            return new TimeOverlapResponse(false, []);
        }

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

    public async Task<IReadOnlyList<AssignedActivityResponse>> GetAssignedAsync(
        Guid userId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    )
    {
        var assignments = await activities.GetUserAssignmentsAsync(userId, startDate, endDate, ct);
        return assignments
            .Select(x => new AssignedActivityResponse(
                x.ActivityId,
                x.Activity.Title,
                x.Activity.Description,
                x.Activity.ActivityStartsAt,
                x.Activity.ActivityEndsAt,
                x.Activity.EventId,
                new AssignedActivityRoleResponse(
                    x.ActivityRoleTypeId,
                    x.ActivityRoleType?.Name ?? string.Empty
                ),
                new AssignedActivityStatusResponse(
                    x.AssignmentStatusId,
                    x.AssignmentStatus?.Name ?? string.Empty
                )
            ))
            .ToList();
    }

    public async Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> GetHouseholdAssignmentsAsync(
        Guid actingUserId,
        Guid eventId,
        CancellationToken ct = default
    )
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

    private static void ApplyAllowedRoles(
        Activity activity,
        IReadOnlyList<ActivityAllowedRoleRequest>? roles
    )
    {
        if (roles is null)
        {
            return;
        }

        foreach (var role in roles.DistinctBy(r => r.ActivityRoleTypeId))
        {
            activity.AllowedRoleTypes.Add(
                new ActivityAllowedRoleType
                {
                    ActivityId = activity.Id,
                    ActivityRoleTypeId = role.ActivityRoleTypeId,
                    DesiredSignups = role.DesiredSignups,
                }
            );
        }
    }

    private static bool Overlaps(Activity a, Activity b)
    {
        return a.ActivityStartsAt is { } aStart
            && a.ActivityEndsAt is { } aEnd
            && b.ActivityStartsAt is { } bStart
            && b.ActivityEndsAt is { } bEnd
            && aStart < bEnd
            && bStart < aEnd;
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
        {
            return Error.Validation();
        }

        return Result.Success();
    }

    public async Task<IReadOnlyList<ActivityRoleTypeResponse>> GetActivityRoleTypesAsync(
        CancellationToken ct = default
    )
    {
        var list = await roleTypes.GetAllAsync(ct);
        return list.OrderBy(x => x.Name).Select(roleType => roleType.ToResponse()).ToList();
    }

    public async Task<ActivityRoleTypeResponse> CreateActivityRoleTypeAsync(
        CreateActivityRoleTypeRequest request,
        CancellationToken ct = default
    )
    {
        var roleType = new ActivityRoleType
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
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
        if (roleType is null)
        {
            return Error.NotFound();
        }

        roleType.Name = request.Name.Trim();
        roleType.Description = request.Description.Trim();
        roleTypes.Update(roleType);
        await uow.SaveChangesAsync(ct);
        return roleType.ToResponse();
    }

    public async Task<Result> DeleteActivityRoleTypeAsync(Guid id, CancellationToken ct = default)
    {
        if (!await roleTypes.ExistsAsync(x => x.Id == id, ct))
        {
            return Error.NotFound();
        }

        await roleTypes.RemoveAsync(x => x.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<AssignmentStatusTypeResponse>> GetAssignmentStatusTypesAsync(
        CancellationToken ct = default
    )
    {
        var list = await statuses.GetAllAsync(ct);
        return list.OrderBy(x => x.Name).Select(statusType => statusType.ToResponse()).ToList();
    }
}
