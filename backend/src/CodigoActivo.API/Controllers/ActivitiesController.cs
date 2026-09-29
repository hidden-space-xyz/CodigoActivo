using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing activities.
/// </summary>
[ApiController]
[Route("api/activities")]
public class ActivitiesController : ApiControllerBase
{
    /// <summary>
    /// Lists the activities that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged activity, or an error response.</returns>
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

    /// <summary>
    /// Gets the requested activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an activity, or an error response.</returns>
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

    /// <summary>
    /// Checks whether the user's assigned activities overlap the selected activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a time overlap, or an error response.</returns>
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

    /// <summary>
    /// Executes the household assignments endpoint for activities.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a household member assignment response>>, or an error response.</returns>
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

    /// <summary>
    /// Executes the role types endpoint for activities.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an activity role type, or an error response.</returns>
    [HttpGet("roleType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityRoleTypeResponse>>> RoleTypesAsync(
        [FromServices] ListActivityRoleTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListActivityRoleTypesQuery(), ct));
    }

    /// <summary>
    /// Executes the signup roles endpoint for activities.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a household signup roles, or an error response.</returns>
    [HttpGet("signup-roles")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<HouseholdSignupRolesResponse>>> SignupRolesAsync(
        [FromServices] GetHouseholdSignupRolesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetHouseholdSignupRolesQuery(UserId), ct));
    }

    /// <summary>
    /// Assigns ment status types according to the validated request.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assignment status type response>>, or an error response.</returns>
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

    /// <summary>
    /// Executes the modality types endpoint for activities.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an activity modality type, or an error response.</returns>
    [HttpGet("modality-types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ActivityModalityTypeResponse>>> ModalityTypesAsync(
        [FromServices] ListActivityModalityTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListActivityModalityTypesQuery(), ct));
    }

    /// <summary>
    /// Creates an activity from the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the created activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an activity, or an error response.</returns>
    [HttpPost("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityResponse>> CreateAsync(
        Guid eventId,
        [FromBody] CreateActivityRequest request,
        [FromServices] CreateActivityCommandHandler handler,
        [FromServices] GetActivityByIdQueryHandler getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(new CreateActivityCommand(eventId, request, UserId), ct),
            id => getById.HandleAsync(new GetActivityByIdQuery(id), ct),
            id => $"/api/activities/{id}"
        );
    }

    /// <summary>
    /// Updates the selected activity with the validated request.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an activity, or an error response.</returns>
    [HttpPut("{activityId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ActivityResponse>> UpdateAsync(
        Guid activityId,
        [FromBody] UpdateActivityRequest request,
        [FromServices] UpdateActivityCommandHandler handler,
        [FromServices] GetActivityByIdQueryHandler getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(new UpdateActivityCommand(activityId, request, UserId), ct),
            () => getById.HandleAsync(new GetActivityByIdQuery(activityId), ct)
        );
    }

    /// <summary>
    /// Deletes the selected activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
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

    /// <summary>
    /// Assigns the selected user to the activity with the requested role.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getAssignment">Query handler that reads the resulting assignment.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assignment, or an error response.</returns>
    [HttpPatch("{activityId:guid}/{userId:guid}/assign")]
    [AllowOnlySelf]
    public async Task<ActionResult<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        [FromBody] AssignRequest request,
        [FromServices] AssignActivityCommandHandler handler,
        [FromServices] GetAssignmentQueryHandler getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new AssignActivityCommand(activityId, userId, UserId, request, IsAdmin),
                ct
            ),
            () => getAssignment.HandleAsync(new GetAssignmentQuery(activityId, userId), ct)
        );
    }

    /// <summary>
    /// Assigns household according to the validated request.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getAssignments">Query handler that reads the created assignments.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assignment, or an error response.</returns>
    [HttpPost("{activityId:guid}/assign-household")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        [FromBody] AssignHouseholdRequest request,
        [FromServices] AssignHouseholdCommandHandler handler,
        [FromServices] GetAssignmentsQueryHandler getAssignments,
        CancellationToken ct
    )
    {
        return await ToOkListAfterAsync(
            await handler.HandleAsync(
                new AssignHouseholdCommand(activityId, UserId, request, IsAdmin),
                ct
            ),
            userIds => getAssignments.HandleAsync(new GetAssignmentsQuery(activityId, userIds), ct)
        );
    }

    /// <summary>
    /// Removes the selected user's activity assignment.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
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

    /// <summary>
    /// Changes the status to the requested value.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getAssignment">Query handler that reads the resulting assignment.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assignment, or an error response.</returns>
    [HttpPatch("{activityId:guid}/{userId:guid}/change-status")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentStatusRequest request,
        [FromServices] ChangeAssignmentStatusCommandHandler handler,
        [FromServices] GetAssignmentQueryHandler getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new ChangeAssignmentStatusCommand(activityId, userId, request),
                ct
            ),
            () => getAssignment.HandleAsync(new GetAssignmentQuery(activityId, userId), ct)
        );
    }

    /// <summary>
    /// Changes the role to the requested value.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getAssignment">Query handler that reads the resulting assignment.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assignment, or an error response.</returns>
    [HttpPatch("{activityId:guid}/{userId:guid}/change-role")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AssignmentResponse>> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        [FromBody] ChangeAssignmentRoleRequest request,
        [FromServices] ChangeAssignmentRoleCommandHandler handler,
        [FromServices] GetAssignmentQueryHandler getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new ChangeAssignmentRoleCommand(activityId, userId, request),
                ct
            ),
            () => getAssignment.HandleAsync(new GetAssignmentQuery(activityId, userId), ct)
        );
    }
}
