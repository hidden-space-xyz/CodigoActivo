using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Services;

public class ReportService(
    IEventRepository events,
    IActivityRoleTypeRepository roleTypes,
    IActivityRepository activities,
    IUserRepository users,
    IDashboardRepository dashboard,
    IQueryExecutor executor,
    HybridCache cache
) : IReportService
{
    private static readonly SortMap<User> AttendeeSort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("birthDate", u => u.BirthDate)
        .Add("type", u => u.UserType.Name)
        .Add("createdAt", u => u.CreatedAt)
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

    public async Task<PagedResult<EventAttendeeResponse>> ListEventAttendeesAsync(
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
            .Select(u => new AttendeeRow(
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
                    .Select(a => new AttendeeAssignmentRow(
                        a.ActivityId,
                        a.Activity.Title,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt,
                        a.ActivityRoleTypeId,
                        a.ActivityRoleType.Name,
                        a.AssignmentStatusId,
                        a.AssignmentStatus.Name,
                        a.CreatedAt
                    ))
                    .ToList(),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                    )
                    .Select(a => new AssignmentWindow(
                        a.ActivityId,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt
                    ))
                    .ToList()
            ));

        var page = await executor.ToPagedAsync(projected, query.Page, query.PageSize, ct);
        var items = page.Items.Select(ToAttendeeResponse).ToList();
        return new PagedResult<EventAttendeeResponse>(items, page.Total, page.Page, page.PageSize);
    }

    private static EventAttendeeResponse ToAttendeeResponse(AttendeeRow row)
    {
        var assignments = row
            .Assignments.Select(a => new EventAttendeeAssignmentResponse(
                a.ActivityId,
                a.ActivityTitle,
                a.ActivityStartsAt,
                a.ActivityEndsAt,
                a.RoleTypeId,
                a.RoleTypeName,
                a.StatusId,
                a.StatusName,
                a.SignedUpAt,
                a.StatusId != SeedIds.AssignmentStatusTypes.Denied
                    && row.Windows.Exists(w =>
                        w.ActivityId != a.ActivityId
                        && a.ActivityStartsAt < w.EndsAt
                        && w.StartsAt < a.ActivityEndsAt
                    )
            ))
            .ToList();

        return new EventAttendeeResponse(
            row.UserId,
            row.FirstName,
            row.LastName,
            row.Email,
            row.Phone,
            row.BirthDate,
            row.UserTypeName,
            row.UserTypeColor,
            row.Guardian,
            assignments
        );
    }

    private sealed record AttendeeRow(
        Guid UserId,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        DateOnly BirthDate,
        string UserTypeName,
        string UserTypeColor,
        EventAttendeeGuardianResponse? Guardian,
        List<AttendeeAssignmentRow> Assignments,
        List<AssignmentWindow> Windows
    );

    private sealed record AttendeeAssignmentRow(
        Guid ActivityId,
        string ActivityTitle,
        DateTimeOffset ActivityStartsAt,
        DateTimeOffset ActivityEndsAt,
        Guid RoleTypeId,
        string? RoleTypeName,
        Guid StatusId,
        string? StatusName,
        DateTimeOffset SignedUpAt
    );

    private sealed record AssignmentWindow(
        Guid ActivityId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    public async Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await GetEventHeaderAsync(eventId, ct);
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
        var ev = await GetEventHeaderAsync(eventId, ct);
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
        return await cache.GetOrCreateAsync(
            "reports:dashboard",
            async token =>
            {
                var counts = await dashboard.GetCountsAsync(token);
                return new DashboardSummaryResponse(
                    counts.Events,
                    counts.Activities,
                    counts.Resources,
                    counts.Announcements,
                    counts.Partners,
                    counts.Users
                );
            },
            CachePolicies.Dashboard,
            [
                CacheTags.Events,
                CacheTags.Activities,
                CacheTags.Resources,
                CacheTags.Announcements,
                CacheTags.Partners,
                CacheTags.Users,
            ],
            ct
        );
    }

    private Task<EventHeader?> GetEventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
