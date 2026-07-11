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

    [HttpGet("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventAttendeesResponse>> EventAttendees(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventAttendeesAsync(eventId, ct));
    }

    [HttpGet("events/{eventId:guid}/badges")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventBadgesResponse>> EventBadges(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventBadgesAsync(eventId, ct));
    }

    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> Dashboard(CancellationToken ct)
    {
        return Ok(await reports.GetDashboardSummaryAsync(ct));
    }
}
