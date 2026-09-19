using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing reports.
/// </summary>
[ApiController]
[Route("api/reports")]
[EnableRateLimiting(SecurityPolicies.Reports)]
public class ReportsController : ApiControllerBase
{
    /// <summary>
    /// Executes the event summary endpoint for reports.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event summary, or an error response.</returns>
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

    /// <summary>
    /// Executes the event attendees endpoint for reports.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged event attendee, or an error response.</returns>
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

    /// <summary>
    /// Executes the event badges endpoint for reports.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event badges, or an error response.</returns>
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

    /// <summary>
    /// Executes the event roster endpoint for reports.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event roster, or an error response.</returns>
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

    /// <summary>
    /// Executes the dashboard endpoint for reports.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a dashboard summary, or an error response.</returns>
    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardAsync(
        [FromServices] GetDashboardSummaryQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetDashboardSummaryQuery(), ct));
    }

    /// <summary>
    /// Executes the dashboard analytics endpoint for reports.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a dashboard analytics, or an error response.</returns>
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
