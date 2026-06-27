using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(IActivityService activities) : ApiControllerBase
{
    [HttpGet("assigned")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AssignedActivityResponse>>> GetAssigned(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken ct
    )
    {
        return Ok(await activities.GetAssignedAsync(UserId, startDate, endDate, ct));
    }

    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    [Cached(nameof(Activity))]
    public async Task<ActionResult<IReadOnlyList<ActivityResponse>>> ListByEvent(
        Guid eventId,
        CancellationToken ct
    )
    {
        return Ok(await activities.ListByEventAsync(eventId, ct));
    }

    [HttpGet("{eventId:guid}/{activityId:guid}")]
    [AllowAnonymous]
    [Cached(nameof(Activity))]
    public async Task<ActionResult<ActivityResponse>> GetById(
        Guid eventId,
        Guid activityId,
        CancellationToken ct
    )
    {
        return ToOk(await activities.GetByIdAsync(eventId, activityId, ct));
    }

    [HttpPost("{eventId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity), nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<ActivityResponse>> Create(
        Guid eventId,
        [FromBody] CreateActivityRequest request,
        CancellationToken ct
    )
    {
        var result = await activities.CreateAsync(eventId, request, UserId, ct);
        return result.IsFailure
            ? (ActionResult<ActivityResponse>)ToProblem(result.Error!)
            : (ActionResult<ActivityResponse>)
                CreatedAtAction(
                    nameof(GetById),
                    new { eventId, activityId = result.Value.Id },
                    result.Value
                );
    }

    [HttpPut("{activityId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity))]
    public async Task<ActionResult<ActivityResponse>> Update(
        Guid activityId,
        [FromBody] UpdateActivityRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.UpdateAsync(activityId, request, UserId, ct));
    }

    [HttpDelete("{activityId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity), nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid activityId, CancellationToken ct)
    {
        return ToNoContent(await activities.DeleteAsync(activityId, ct));
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/assign")]
    [Authorize]
    [AllowOnlySelf]
    public async Task<ActionResult<AssignmentResponse>> Assign(
        Guid activityId,
        Guid userId,
        [FromBody] AssignRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.AssignAsync(activityId, userId, request, ct));
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/unassign")]
    [Authorize]
    [AllowOnlySelf]
    public async Task<IActionResult> Unassign(Guid activityId, Guid userId, CancellationToken ct)
    {
        return ToNoContent(await activities.UnassignAsync(activityId, userId, ct));
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/change-status")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeStatus(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentStatusRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.ChangeStatusAsync(activityId, userId, request, ct));
    }

    [HttpGet("{activityId:guid}/{userId:guid}/verifyTimeOverlaps")]
    [Authorize]
    [AllowOnlySelf]
    public async Task<ActionResult<TimeOverlapResponse>> VerifyTimeOverlaps(
        Guid activityId,
        Guid userId,
        CancellationToken ct
    )
    {
        return ToOk(await activities.VerifyTimeOverlapsAsync(activityId, userId, ct));
    }

    [HttpGet("roleTypes")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityRoleTypeResponse>>> GetRoleTypes(
        CancellationToken ct
    )
    {
        return Ok(await activities.GetActivityRoleTypesAsync(ct));
    }

    [HttpPost("roleType")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity))]
    public async Task<ActionResult<ActivityRoleTypeResponse>> CreateRoleType(
        [FromBody] CreateActivityRoleTypeRequest request,
        CancellationToken ct
    )
    {
        return Ok(await activities.CreateActivityRoleTypeAsync(request, ct));
    }

    [HttpPut("roleType/{activityRoleTypeId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity))]
    public async Task<ActionResult<ActivityRoleTypeResponse>> UpdateRoleType(
        Guid activityRoleTypeId,
        [FromBody] UpdateActivityRoleTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.UpdateActivityRoleTypeAsync(activityRoleTypeId, request, ct));
    }

    [HttpDelete("roleType/{activityRoleTypeId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Activity))]
    public async Task<IActionResult> DeleteRoleType(Guid activityRoleTypeId, CancellationToken ct)
    {
        return ToNoContent(await activities.DeleteActivityRoleTypeAsync(activityRoleTypeId, ct));
    }

    [HttpGet("assignmentStatusTypes")]
    [AllowOnlyAdmin]
    public async Task<
        ActionResult<IReadOnlyList<AssignmentStatusTypeResponse>>
    > GetAssignmentStatusTypes(CancellationToken ct)
    {
        return Ok(await activities.GetAssignmentStatusTypesAsync(ct));
    }
}
