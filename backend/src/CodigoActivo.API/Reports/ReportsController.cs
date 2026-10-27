using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Reports;

/// <summary>
/// Exposes the read-only reports administrators use: the dashboard and, per event, its summary,
/// attendees, badges and roster.
/// </summary>
[ApiController]
[Route("api/reports")]
[EnableRateLimiting(SecurityPolicies.Reports)]
public class ReportsController : ApiControllerBase
{
    /// <summary>
    /// Summarizes the signups and ratings of an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the event summary, or an error response.</returns>
    [HttpGet("events/{eventId:guid}/summary")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummaryAsync(
        Guid eventId,
        [FromServices] IQueryHandler<GetEventSummaryQuery, Result<EventSummaryResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventSummaryQuery(EventId.From(eventId)), ct));
    }

    /// <summary>
    /// Lists the people signed up to the activities of an event, each with the signups that match
    /// the criteria and their guardian's contact details.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of attendees, or an error response.</returns>
    [HttpGet("events/{eventId:guid}/attendees")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventAttendeeResponse>>> EventAttendeesAsync(
        Guid eventId,
        [FromQuery] EventAttendeeListQuery query,
        [FromServices]
            IQueryHandler<
            ListEventAttendeesQuery,
            Result<PagedResult<EventAttendeeResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new ListEventAttendeesQuery(EventId.From(eventId), query), ct)
        );
    }

    /// <summary>
    /// Returns one badge per person with a confirmed signup to the event, listing their confirmed
    /// activities.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the badges, or an error response.</returns>
    [HttpGet("events/{eventId:guid}/badges")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventBadgesResponse>> EventBadgesAsync(
        Guid eventId,
        [FromServices] IQueryHandler<GetEventBadgesQuery, Result<EventBadgesResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventBadgesQuery(EventId.From(eventId)), ct));
    }

    /// <summary>
    /// Returns the activities of the event with confirmed signups, each listing the people confirmed
    /// in it with their contact details.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the roster, or an error response.</returns>
    [HttpGet("events/{eventId:guid}/roster")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventRosterResponse>> EventRosterAsync(
        Guid eventId,
        [FromServices] IQueryHandler<GetEventRosterQuery, Result<EventRosterResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventRosterQuery(EventId.From(eventId)), ct));
    }

    /// <summary>
    /// Returns the totals of the dashboard, cached for a minute or until a change to one of their
    /// sources evicts them.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the dashboard totals.</returns>
    [HttpGet("dashboard")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardAsync(
        [FromServices] IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetDashboardSummaryQuery(), ct));
    }

    /// <summary>
    /// Returns the analytics of the dashboard for the selected window, cached for a minute per
    /// window or until a change to one of their sources evicts them.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the dashboard analytics.</returns>
    [HttpGet("dashboard/analytics")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<DashboardAnalyticsResponse>> DashboardAnalyticsAsync(
        [FromQuery] DashboardAnalyticsQuery query,
        [FromServices]
            IQueryHandler<GetDashboardAnalyticsQuery, DashboardAnalyticsResponse> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetDashboardAnalyticsQuery(query), ct));
    }
}
