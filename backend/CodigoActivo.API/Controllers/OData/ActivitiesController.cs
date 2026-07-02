using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

public class ActivitiesController(IActivityService activities) : ODataController
{
    [AllowAnonymous]
    [EnableQuery(PageSize = 1000)]
    public IQueryable<ActivityResponse> Get()
    {
        return activities.QueryActivities();
    }

    [AllowAnonymous]
    public async Task<ActionResult<ActivityResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await activities.QueryActivities().FirstOrDefaultAsync(a => a.Id == key, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Computed reads (formerly REST GETs on ActivityCommandsController), now unbound OData
    /// functions: an overlap check for a user's assignments and the caller's household assignments.
    /// </summary>
    [HttpGet("api/odata/VerifyTimeOverlaps(activityId={activityId},userId={userId})")]
    [AllowOnlySelf]
    public async Task<ActionResult<TimeOverlapResponse>> VerifyTimeOverlaps(
        Guid activityId,
        Guid userId,
        CancellationToken ct
    )
    {
        var result = await activities.VerifyTimeOverlapsAsync(activityId, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet("api/odata/HouseholdAssignments(eventId={eventId})")]
    [Authorize]
    public async Task<
        ActionResult<IEnumerable<HouseholdMemberAssignmentResponse>>
    > HouseholdAssignments(Guid eventId, CancellationToken ct)
    {
        var userId =
            User.GetUserId()
            ?? throw new InvalidOperationException("No authenticated user on this request.");
        return Ok(await activities.GetHouseholdAssignmentsAsync(userId, eventId, ct));
    }
}
