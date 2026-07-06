using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class ReportService(
    IEventRepository events,
    IActivityRoleTypeRepository roleTypes,
    IAssignmentStatusTypeRepository statusTypes,
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
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        var assignments = ev.Activities.SelectMany(a => a.Assignments).ToList();

        var roleNames = (await roleTypes.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.Name);

        var approvedByRole = assignments
            .Where(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed)
            .GroupBy(a => a.ActivityRoleTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var roleTypeBreakdown = ev
            .Activities.SelectMany(a => a.AllowedRoleTypes)
            .Select(r => r.ActivityRoleTypeId)
            .Distinct()
            .Select(id => new EventRoleTypeSummaryResponse(
                id,
                roleNames.GetValueOrDefault(id),
                approvedByRole.GetValueOrDefault(id, 0)
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

    public async Task<Result<EventAssignmentsReportResponse>> GetEventAssignmentsAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.GetWithActivitiesAndAssignmentsAsync(eventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        var roleNames = (await roleTypes.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.Name);
        var statusNames = (await statusTypes.GetAllAsync(ct)).ToDictionary(s => s.Id, s => s.Name);

        var items = ev
            .Activities.SelectMany(a =>
                a.Assignments.Select(asg => new AssignmentReportItemResponse(
                    a.Id,
                    a.Title,
                    asg.UserId,
                    asg.ActivityRoleTypeId,
                    roleNames.GetValueOrDefault(asg.ActivityRoleTypeId),
                    asg.AssignmentStatusId,
                    statusNames.GetValueOrDefault(asg.AssignmentStatusId)
                ))
            )
            .ToList();

        return new EventAssignmentsReportResponse(ev.Id, ev.Title, items);
    }

    public async Task<Result<ActivityAssignmentsReportResponse>> GetActivityAssignmentsAsync(
        Guid activityId,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetWithAssignmentsAndUsersAsync(activityId, ct);
        if (activity is null) return Error.NotFound(ErrorCode.ActivityNotFound);

        var signedUpUserIds = activity.Assignments.Select(a => a.UserId).ToHashSet();

        var rows = activity
            .Assignments.Select(a => new ActivityAssignmentRowResponse(
                a.User.Id,
                a.User.FirstName,
                a.User.LastName,
                a.User.Email,
                a.User.Phone,
                a.User.BirthDate,
                a.User.ParentId,
                true,
                a.ActivityRoleTypeId,
                a.ActivityRoleType.Name,
                a.AssignmentStatusId,
                a.AssignmentStatus.Name
            ))
            .ToList();

        var addedParents = new HashSet<Guid>();
        foreach (var assignment in activity.Assignments)
        {
            var parent = assignment.User.Parent;
            if (
                parent is null
                || signedUpUserIds.Contains(parent.Id)
                || !addedParents.Add(parent.Id)
            )
            {
                continue;
            }

            rows.Add(
                new ActivityAssignmentRowResponse(
                    parent.Id,
                    parent.FirstName,
                    parent.LastName,
                    parent.Email,
                    parent.Phone,
                    parent.BirthDate,
                    parent.ParentId,
                    false,
                    null,
                    null,
                    null,
                    null
                )
            );
        }

        var roleTypeBreakdown = activity
            .AllowedRoleTypes.Select(ar => new ActivityRoleTypeSummaryResponse(
                ar.ActivityRoleTypeId,
                ar.ActivityRoleType.Name,
                activity.Assignments.Count(a =>
                    a.ActivityRoleTypeId == ar.ActivityRoleTypeId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
            ))
            .OrderBy(r => r.RoleTypeName, StringComparer.Ordinal)
            .ToList();

        return new ActivityAssignmentsReportResponse(
            activity.Id,
            activity.Title,
            activity.Assignments.Count,
            roleTypeBreakdown,
            rows
        );
    }

    public async Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.FindAsync(e => e.Id == eventId, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

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

        var badges = rows
            .GroupBy(r => r.UserId)
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