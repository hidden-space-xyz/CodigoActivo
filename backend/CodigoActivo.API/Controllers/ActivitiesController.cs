using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(IActivityService activities) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ActivityResponse>>> List(
        [FromQuery] ActivityListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await activities.ListAsync(query, ct));
    }

    [HttpGet("{activityId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ActivityResponse>> Get(Guid activityId, CancellationToken ct)
    {
        return ToOk(await activities.GetByIdAsync(activityId, ct));
    }

    [HttpGet("{activityId:guid}/overlaps/{userId:guid}")]
    [AllowOnlySelf]
    public async Task<ActionResult<TimeOverlapResponse>> Overlaps(
        Guid activityId,
        Guid userId,
        CancellationToken ct
    )
    {
        return ToOk(await activities.VerifyTimeOverlapsAsync(activityId, userId, ct));
    }

    [HttpGet("household-assignments/{eventId:guid}")]
    [Authorize]
    public async Task<
        ActionResult<IReadOnlyList<HouseholdMemberAssignmentResponse>>
    > HouseholdAssignments(Guid eventId, CancellationToken ct)
    {
        return Ok(await activities.GetHouseholdAssignmentsAsync(UserId, eventId, ct));
    }

    [HttpGet("roleType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityRoleTypeResponse>>> RoleTypes(
        CancellationToken ct
    )
    {
        return Ok(await activities.ListRoleTypesAsync(ct));
    }

    [HttpGet("assignment-status-types")]
    [AllowOnlyAdmin]
    public async Task<
        ActionResult<IReadOnlyList<AssignmentStatusTypeResponse>>
    > AssignmentStatusTypes(CancellationToken ct)
    {
        return Ok(await activities.ListAssignmentStatusTypesAsync(ct));
    }

    [HttpGet("modality-types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityModalityTypeResponse>>> ModalityTypes(
        CancellationToken ct
    )
    {
        return Ok(await activities.ListModalityTypesAsync(ct));
    }

    [HttpPost("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityResponse>> Create(
        Guid eventId,
        [FromBody] CreateActivityRequest request,
        CancellationToken ct
    )
    {
        var result = await activities.CreateAsync(eventId, request, UserId, ct);
        if (result.IsFailure) return ToProblem(result.Error!);

        return Created($"/api/activities/{result.Value.Id}", result.Value);
    }

    [HttpPut("{activityId:guid}")]
    [AllowOnlyAdmin]
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
        return ToOk(await activities.AssignAsync(activityId, userId, request, IsAdmin, ct));
    }

    [HttpPost("{activityId:guid}/assign-household")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AssignmentResponse>>> AssignHousehold(
        Guid activityId,
        [FromBody] AssignHouseholdRequest request,
        CancellationToken ct
    )
    {
        return ToOk(
            await activities.AssignHouseholdAsync(activityId, UserId, request, IsAdmin, ct)
        );
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/unassign")]
    [Authorize]
    [AllowOnlySelf]
    public async Task<IActionResult> Unassign(Guid activityId, Guid userId, CancellationToken ct)
    {
        return ToNoContent(await activities.UnassignAsync(activityId, userId, IsAdmin, ct));
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

    [HttpPatch("{activityId:guid}/{userId:guid}/change-role")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeRole(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentRoleRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.ChangeRoleAsync(activityId, userId, request, ct));
    }

    [HttpPost("roleType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityRoleTypeResponse>> CreateRoleType(
        [FromBody] CreateActivityRoleTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await activities.CreateActivityRoleTypeAsync(request, ct));
    }

    [HttpPut("roleType/{activityRoleTypeId:guid}")]
    [AllowOnlyAdmin]
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
    public async Task<IActionResult> DeleteRoleType(Guid activityRoleTypeId, CancellationToken ct)
    {
        return ToNoContent(await activities.DeleteActivityRoleTypeAsync(activityRoleTypeId, ct));
    }
}
