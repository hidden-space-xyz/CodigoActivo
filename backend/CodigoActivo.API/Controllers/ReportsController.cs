using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("events/{eventId:guid}/summary")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummary(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventSummaryAsync(eventId, ct));
    }

    [HttpGet("events/{eventId:guid}/assignments")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventAssignmentsReportResponse>> EventAssignments(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventAssignmentsAsync(eventId, ct));
    }

    [HttpGet("activities/{activityId:guid}/assignments")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityAssignmentsReportResponse>> ActivityAssignments(
        Guid activityId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetActivityAssignmentsAsync(activityId, ct));
    }

    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> Dashboard(CancellationToken ct)
    {
        return Ok(await reports.GetDashboardSummaryAsync(ct));
    }
}
