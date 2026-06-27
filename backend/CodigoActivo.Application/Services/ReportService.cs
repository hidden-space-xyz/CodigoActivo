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
    IUserRepository users
) : IReportService
{
    public async Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.GetWithActivitiesAndAssignmentsAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

        var assignments = ev.Activities.SelectMany(a => a.Assignments).ToList();

        return new EventSummaryResponse(
            ev.Id,
            ev.Title,
            ev.Activities.Count,
            assignments.Count,
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested),
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed),
            assignments.Count(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied),
            assignments.Select(a => a.UserId).Distinct().Count()
        );
    }

    public async Task<Result<EventAssignmentsReportResponse>> GetEventAssignmentsAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await events.GetWithActivitiesAndAssignmentsAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

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
