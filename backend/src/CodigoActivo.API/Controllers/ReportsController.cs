using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    [HttpGet("events/{eventId:guid}/summary")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummaryAsync(
        Guid eventId,
        [FromServices] GetEventSummaryQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventSummaryQuery(eventId), ct));
    }

    [HttpGet("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventAttendeeResponse>>> EventAttendeesAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromServices] ListEventAttendeesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListEventAttendeesQuery(eventId, query), ct));
    }

    [HttpGet("events/{eventId:guid}/badges")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventBadgesResponse>> EventBadgesAsync(
        Guid eventId,
        [FromServices] GetEventBadgesQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventBadgesQuery(eventId), ct));
    }

    [HttpGet("events/{eventId:guid}/roster")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventRosterResponse>> EventRosterAsync(
        Guid eventId,
        [FromServices] GetEventRosterQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventRosterQuery(eventId), ct));
    }

    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardAsync(
        [FromServices] GetDashboardSummaryQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetDashboardSummaryQuery(), ct));
    }

    [HttpGet("dashboard/analytics")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardAnalyticsResponse>> DashboardAnalyticsAsync(
        [FromQuery] DashboardAnalyticsQuery query,
        [FromServices] GetDashboardAnalyticsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetDashboardAnalyticsQuery(query), ct));
    }
}
