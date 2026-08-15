using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Activities)]
    public async Task<ActionResult<PagedResult<ActivityResponse>>> ListAsync(
        [FromQuery] ActivityListQuery query,
        [FromServices] ListActivitiesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListActivitiesQuery(query), ct));
    }

    [HttpGet("{activityId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Activities)]
    public async Task<ActionResult<ActivityResponse>> GetAsync(
        Guid activityId,
        [FromServices] GetActivityByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetActivityByIdQuery(activityId), ct));
    }

    [HttpGet("{activityId:guid}/overlaps/{userId:guid}")]
    [AllowOnlySelf]
    public async Task<ActionResult<TimeOverlapResponse>> OverlapsAsync(
        Guid activityId,
        Guid userId,
        [FromServices] VerifyTimeOverlapsQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new VerifyTimeOverlapsQuery(activityId, userId), ct));
    }

    [HttpGet("household-assignments/{eventId:guid}")]
    [Authorize]
    public async Task<
        ActionResult<IReadOnlyList<HouseholdMemberAssignmentResponse>>
    > HouseholdAssignmentsAsync(
        Guid eventId,
        [FromServices] GetHouseholdAssignmentsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetHouseholdAssignmentsQuery(UserId, eventId), ct));
    }

    [HttpGet("roleType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityRoleTypeResponse>>> RoleTypesAsync(
        [FromServices] ListActivityRoleTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListActivityRoleTypesQuery(), ct));
    }

    [HttpGet("signup-roles")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<HouseholdSignupRolesResponse>>> SignupRolesAsync(
        [FromServices] GetHouseholdSignupRolesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetHouseholdSignupRolesQuery(UserId), ct));
    }

    [HttpGet("assignment-status-types")]
    [AllowOnlyAdmin]
    public async Task<
        ActionResult<IReadOnlyList<AssignmentStatusTypeResponse>>
    > AssignmentStatusTypesAsync(
        [FromServices] ListAssignmentStatusTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListAssignmentStatusTypesQuery(), ct));
    }

    [HttpGet("modality-types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityModalityTypeResponse>>> ModalityTypesAsync(
        [FromServices] ListActivityModalityTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListActivityModalityTypesQuery(), ct));
    }

    [HttpPost("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityResponse>> CreateAsync(
        Guid eventId,
        [FromBody] CreateActivityRequest request,
        [FromServices] CreateActivityCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateActivityCommand(eventId, request, UserId), ct),
            a => $"/api/activities/{a.Id}"
        );
    }

    [HttpPut("{activityId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityResponse>> UpdateAsync(
        Guid activityId,
        [FromBody] UpdateActivityRequest request,
        [FromServices] UpdateActivityCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateActivityCommand(activityId, request, UserId), ct)
        );
    }

    [HttpDelete("{activityId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid activityId,
        [FromServices] DeleteActivityCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteActivityCommand(activityId), ct));
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/assign")]
    [AllowOnlySelf]
    public async Task<ActionResult<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        [FromBody] AssignRequest request,
        [FromServices] AssignActivityCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new AssignActivityCommand(activityId, userId, UserId, request, IsAdmin),
                ct
            )
        );
    }

    [HttpPost("{activityId:guid}/assign-household")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        [FromBody] AssignHouseholdRequest request,
        [FromServices] AssignHouseholdCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new AssignHouseholdCommand(activityId, UserId, request, IsAdmin),
                ct
            )
        );
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/unassign")]
    [AllowOnlySelf]
    public async Task<IActionResult> UnassignAsync(
        Guid activityId,
        Guid userId,
        [FromServices] UnassignActivityCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new UnassignActivityCommand(activityId, userId, IsAdmin), ct)
        );
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/change-status")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentStatusRequest request,
        [FromServices] ChangeAssignmentStatusCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new ChangeAssignmentStatusCommand(activityId, userId, request),
                ct
            )
        );
    }

    [HttpPatch("{activityId:guid}/{userId:guid}/change-role")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentRoleRequest request,
        [FromServices] ChangeAssignmentRoleCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new ChangeAssignmentRoleCommand(activityId, userId, request),
                ct
            )
        );
    }
}
