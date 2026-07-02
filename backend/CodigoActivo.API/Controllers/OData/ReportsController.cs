using CodigoActivo.API.Attributes;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

/// <summary>
/// Server-computed reports exposed as unbound OData functions (registered in
/// <see cref="OData.EdmModelBuilder"/>). They are reads, so they belong on an OData controller,
/// but they are aggregates rather than entity collections, hence functions instead of entity sets.
/// </summary>
public class ReportsController(IReportService reports) : ODataController
{
    [HttpGet("api/odata/EventSummary(eventId={eventId})")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventSummaryResponse>> EventSummary(
        Guid eventId,
        CancellationToken ct
    )
    {
        var result = await reports.GetEventSummaryAsync(eventId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet("api/odata/EventAssignments(eventId={eventId})")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventAssignmentsReportResponse>> EventAssignments(
        Guid eventId,
        CancellationToken ct
    )
    {
        var result = await reports.GetEventAssignmentsAsync(eventId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet("api/odata/ActivityAssignments(activityId={activityId})")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityAssignmentsReportResponse>> ActivityAssignments(
        Guid activityId,
        CancellationToken ct
    )
    {
        var result = await reports.GetActivityAssignmentsAsync(activityId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet("api/odata/DashboardSummary()")]
    [AllowAnonymous]
    public async Task<ActionResult<DashboardSummaryResponse>> DashboardSummary(CancellationToken ct)
    {
        return Ok(await reports.GetDashboardSummaryAsync(ct));
    }
}
