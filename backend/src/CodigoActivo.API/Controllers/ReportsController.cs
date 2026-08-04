using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("events/{eventId:guid}/summary")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummaryAsync(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventSummaryAsync(eventId, ct));
    }

    [HttpGet("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventAttendeeResponse>>> EventAttendeesAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await reports.ListEventAttendeesAsync(eventId, query, ct));
    }

    [HttpGet("events/{eventId:guid}/badges")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventBadgesResponse>> EventBadgesAsync(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventBadgesAsync(eventId, ct));
    }

    [HttpGet("events/{eventId:guid}/roster")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventRosterResponse>> EventRosterAsync(
        Guid eventId,
        CancellationToken ct
    )
    {
        return ToOk(await reports.GetEventRosterAsync(eventId, ct));
    }

    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardAsync(CancellationToken ct)
    {
        return Ok(await reports.GetDashboardSummaryAsync(ct));
    }

    [HttpGet("dashboard/analytics")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardAnalyticsResponse>> DashboardAnalyticsAsync(
        [FromQuery] DashboardAnalyticsQuery query,
        CancellationToken ct
    )
    {
        return Ok(await reports.GetDashboardAnalyticsAsync(query, ct));
    }
}
