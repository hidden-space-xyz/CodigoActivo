using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
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
    public async Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.GetWithActivitiesAndAssignmentsAsync(eventId, ct);
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var assignments = ev.Activities.SelectMany(a => a.Assignments).ToList();

        var approvedByRole = assignments
            .Where(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed)
            .GroupBy(a => a.ActivityRoleTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var roleTypeBreakdown = (await roleTypes.GetAllAsync(ct))
            .Select(role => new EventRoleTypeSummaryResponse(
                role.Id,
                role.Name,
                approvedByRole.GetValueOrDefault(role.Id, 0)
            ))
            .OrderBy(r => r.RoleTypeName, StringComparer.Ordinal)
            .ToList();

        return new EventSummaryResponse(
            ev.Id,
            ev.Title,
            ev.Activities.Count,
            assignments.Count,
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested),
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed),
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied),
            assignments.Select(a => a.UserId).Distinct().Count(),
            roleTypeBreakdown
        );
    }

    public async Task<Result<EventAttendeesResponse>> GetEventAttendeesAsync(
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
                .Where(a => a.Activity.EventId == eventId)
                .Select(a => new
                {
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.Email,
                    a.User.Phone,
                    a.User.BirthDate,
                    UserTypeName = a.User.UserType.Name,
                    UserTypeColor = a.User.UserType.Color,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventAttendeeGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Email,
                            a.User.Parent.Phone
                        ),
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt,
                    a.ActivityRoleTypeId,
                    RoleTypeName = a.ActivityRoleType.Name,
                    a.AssignmentStatusId,
                    StatusName = a.AssignmentStatus.Name,
                    SignedUpAt = a.CreatedAt,
                }),
            ct
        );

        var attendees = rows.GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First();
                var items = g.OrderBy(r => r.ActivityStartsAt)
                    .ThenBy(r => r.ActivityTitle, StringComparer.Ordinal)
                    .ToList();
                return new EventAttendeeResponse(
                    g.Key,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Phone,
                    user.BirthDate,
                    user.UserTypeName,
                    user.UserTypeColor,
                    user.Guardian,
                    items
                        .Select(r => new EventAttendeeAssignmentResponse(
                            r.ActivityId,
                            r.ActivityTitle,
                            r.ActivityStartsAt,
                            r.ActivityEndsAt,
                            r.ActivityRoleTypeId,
                            r.RoleTypeName,
                            r.AssignmentStatusId,
                            r.StatusName,
                            r.SignedUpAt,
                            r.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                                && items.Exists(other =>
                                    other.ActivityId != r.ActivityId
                                    && other.AssignmentStatusId
                                        != SeedIds.AssignmentStatusTypes.Denied
                                    && r.ActivityStartsAt < other.ActivityEndsAt
                                    && other.ActivityStartsAt < r.ActivityEndsAt
                                )
                        ))
                        .ToList()
                );
            })
            .OrderBy(a => a.LastName, StringComparer.Ordinal)
            .ThenBy(a => a.FirstName, StringComparer.Ordinal)
            .ThenBy(a => a.UserId)
            .ToList();

        return new EventAttendeesResponse(ev.Id, ev.Title, attendees);
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
            .OrderBy(b => b.LastName, StringComparer.Ordinal)
            .ThenBy(b => b.FirstName, StringComparer.Ordinal)
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
                        .ThenBy(r => r.FirstName, StringComparer.Ordinal)
                        .ThenBy(r => r.LastName, StringComparer.Ordinal)
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
