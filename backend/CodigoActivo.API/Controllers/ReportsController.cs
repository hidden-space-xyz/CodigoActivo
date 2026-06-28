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
    [HttpGet("event/{eventId:guid}/summary")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummary(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventSummaryAsync(eventId, ct));
    }

    [HttpGet("event/{eventId:guid}/assignments")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventAssignmentsReportResponse>> EventAssignments(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventAssignmentsAsync(eventId, ct));
    }

    [HttpGet("activity/{activityId:guid}/assignments")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityAssignmentsReportResponse>> ActivityAssignments(
        Guid activityId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetActivityAssignmentsAsync(activityId, ct));
    }

    [HttpGet("dashboard/summary-counters")]
    [AllowAnonymous]
    [Cached(nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardSummary(CancellationToken ct)
    {
        return Ok(await reports.GetDashboardSummaryAsync(ct));
    }
}
