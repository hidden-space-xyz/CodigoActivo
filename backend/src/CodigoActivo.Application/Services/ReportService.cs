using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class ReportService(
    IEventRepository events,
    IActivityRoleTypeRepository roleTypes,
    IActivityRepository activities,
    IResourceRepository resources,
    IAnnouncementRepository announcements,
    IPartnerRepository partners,
    IUserRepository users,
    IQueryExecutor executor
) : IReportService
{
    private static readonly SortMap<User> AttendeeSort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Default("firstName")
        .Tie(u => u.Id);

    public async Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await executor.FirstOrDefaultAsync(
            events
                .Query()
                .Where(e => e.Id == eventId)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    ActivitiesCount = e.Activities.Count,
                }),
            ct
        );
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var stats = await executor.FirstOrDefaultAsync(
            activities
                .QueryAssignments()
                .Where(a => a.Activity.EventId == eventId)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Requested = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested
                    ),
                    Confirmed = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ),
                    Denied = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied
                    ),
                    DistinctUsers = g.Select(a => a.UserId).Distinct().Count(),
                }),
            ct
        );

        var roleTypeBreakdown = await executor.ToListAsync(
            roleTypes
                .Query()
                .OrderBy(role => role.Name)
                .Select(role => new EventRoleTypeSummaryResponse(
                    role.Id,
                    role.Name,
                    role.Assignments.Count(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    )
                )),
            ct
        );

        return new EventSummaryResponse(
            ev.Id,
            ev.Title,
            ev.ActivitiesCount,
            stats?.Total ?? 0,
            stats?.Requested ?? 0,
            stats?.Confirmed ?? 0,
            stats?.Denied ?? 0,
            stats?.DistinctUsers ?? 0,
            roleTypeBreakdown
        );
    }

    public Task<PagedResult<EventAttendeeResponse>> ListEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        CancellationToken ct = default
    )
    {
        var activityId = query.ActivityId;
        var roleTypeId = query.RoleTypeId;
        var statusId = query.StatusId;

        var source = users
            .Query()
            .Where(u =>
                u.Assignments.Any(a =>
                    a.Activity.EventId == eventId
                    && (activityId == null || a.ActivityId == activityId)
                    && (roleTypeId == null || a.ActivityRoleTypeId == roleTypeId)
                    && (statusId == null || a.AssignmentStatusId == statusId)
                )
            );

        if (query.UserTypeId is { } userTypeId)
            source = source.Where(u => u.UserTypeId == userTypeId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            source = source.Where(
                TextSearch.Contains<User>(
                    u =>
                        u.FirstName
                        + " "
                        + u.LastName
                        + " "
                        + (u.Email ?? "")
                        + " "
                        + (u.Phone ?? "")
                        + (
                            u.Parent == null
                                ? ""
                                : " "
                                    + u.Parent.FirstName
                                    + " "
                                    + u.Parent.LastName
                                    + " "
                                    + (u.Parent.Email ?? "")
                                    + " "
                                    + (u.Parent.Phone ?? "")
                        ),
                    TextSearch.Normalize(query.Search)
                )
            );
        }

        var projected = AttendeeSort
            .Apply(source, query.Sort)
            .Select(u => new EventAttendeeResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.BirthDate,
                u.UserType.Name,
                u.UserType.Color,
                u.Parent == null
                    ? null
                    : new EventAttendeeGuardianResponse(
                        u.Parent.FirstName,
                        u.Parent.LastName,
                        u.Parent.Email,
                        u.Parent.Phone
                    ),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && (activityId == null || a.ActivityId == activityId)
                        && (roleTypeId == null || a.ActivityRoleTypeId == roleTypeId)
                        && (statusId == null || a.AssignmentStatusId == statusId)
                    )
                    .OrderBy(a => a.Activity.ActivityStartsAt)
                    .ThenBy(a => a.Activity.Title)
                    .Select(a => new EventAttendeeAssignmentResponse(
                        a.ActivityId,
                        a.Activity.Title,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt,
                        a.ActivityRoleTypeId,
                        a.ActivityRoleType.Name,
                        a.AssignmentStatusId,
                        a.AssignmentStatus.Name,
                        a.CreatedAt,
                        a.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                            && u.Assignments.Any(other =>
                                other.ActivityId != a.ActivityId
                                && other.Activity.EventId == eventId
                                && other.AssignmentStatusId
                                    != SeedIds.AssignmentStatusTypes.Denied
                                && a.Activity.ActivityStartsAt < other.Activity.ActivityEndsAt
                                && other.Activity.ActivityStartsAt < a.Activity.ActivityEndsAt
                            )
                    ))
                    .ToList()
            ));

        return executor.ToPagedAsync(projected, query.Page, query.PageSize, ct);
    }

    public async Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    UserTypeName = a.User.UserType.Name,
                    UserTypeColor = a.User.UserType.Color,
                    a.User.CreatedAt,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventBadgeGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Phone
                        ),
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.ActivityStartsAt,
                }),
            ct
        );

        var badges = rows.GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First();
                return new EventBadgeResponse(
                    g.Key,
                    user.FirstName,
                    user.LastName,
                    user.UserTypeName,
                    user.UserTypeColor,
                    user.CreatedAt,
                    user.Guardian,
                    g.OrderBy(r => r.ActivityStartsAt)
                        .ThenBy(r => r.ActivityTitle, StringComparer.Ordinal)
                        .DistinctBy(r => r.ActivityId)
                        .Select(r => r.ActivityTitle)
                        .ToList()
                );
            })
            .OrderBy(b => TextSearch.Normalize(b.LastName), StringComparer.Ordinal)
            .ThenBy(b => TextSearch.Normalize(b.FirstName), StringComparer.Ordinal)
            .ThenBy(b => b.UserId)
            .ToList();

        return new EventBadgesResponse(ev.Id, ev.Title, badges);
    }

    public async Task<Result<EventRosterResponse>> GetEventRosterAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.Location,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt,
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.BirthDate,
                    a.User.Email,
                    a.User.Phone,
                    a.ActivityRoleTypeId,
                    RoleName = a.ActivityRoleType.Name,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventRosterGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Email,
                            a.User.Parent.Phone
                        ),
                }),
            ct
        );

        var rosterActivities = rows.GroupBy(r => r.ActivityId)
            .Select(g =>
            {
                var activity = g.First();
                return new EventRosterActivityResponse(
                    g.Key,
                    activity.ActivityTitle,
                    activity.Location,
                    activity.ActivityStartsAt,
                    activity.ActivityEndsAt,
                    g.OrderBy(r => RosterRolePriority(r.ActivityRoleTypeId))
                        .ThenBy(r => TextSearch.Normalize(r.FirstName), StringComparer.Ordinal)
                        .ThenBy(r => TextSearch.Normalize(r.LastName), StringComparer.Ordinal)
                        .ThenBy(r => r.UserId)
                        .DistinctBy(r => r.UserId)
                        .Select(r => new EventRosterParticipantResponse(
                            r.UserId,
                            r.FirstName,
                            r.LastName,
                            r.BirthDate,
                            r.Email,
                            r.Phone,
                            r.RoleName,
                            r.Guardian
                        ))
                        .ToList()
                );
            })
            .OrderBy(a => a.ActivityStartsAt)
            .ThenBy(a => a.Title, StringComparer.Ordinal)
            .ThenBy(a => a.ActivityId)
            .ToList();

        return new EventRosterResponse(ev.Id, ev.Title, rosterActivities);
    }

    private static int RosterRolePriority(Guid roleTypeId)
    {
        if (roleTypeId == SeedIds.ActivityRoleTypes.Leader)
            return 0;
        if (roleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            return 1;
        return roleTypeId == SeedIds.ActivityRoleTypes.Participant ? 2 : 3;
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        CancellationToken ct = default
    )
    {
        return new DashboardSummaryResponse(
            await events.CountAsync(_ => true, ct),
            await activities.CountAsync(_ => true, ct),
            await resources.CountAsync(_ => true, ct),
            await announcements.CountAsync(_ => true, ct),
            await partners.CountAsync(_ => true, ct),
            await users.CountAsync(_ => true, ct)
        );
    }
}
