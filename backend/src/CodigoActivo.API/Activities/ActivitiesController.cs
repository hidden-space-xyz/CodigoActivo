using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using EventId = CodigoActivo.Domain.Events.EventId;
using UserId = CodigoActivo.Domain.Users.UserId;

namespace CodigoActivo.API.Activities;

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
        [FromServices] IQueryHandler<ListActivitiesQuery, PagedResult<ActivityResponse>> handler,
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
        [FromServices] IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new GetActivityByIdQuery(ActivityId.From(activityId)), ct)
        );
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
        [FromServices] IQueryHandler<VerifyTimeOverlapsQuery, Result<TimeOverlapResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new VerifyTimeOverlapsQuery(ActivityId.From(activityId), UserId.From(userId)),
                ct
            )
        );
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
        [FromServices]
            IQueryHandler<
            GetHouseholdAssignmentsQuery,
            IReadOnlyList<HouseholdMemberAssignmentResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(
            await handler.HandleAsync(new GetHouseholdAssignmentsQuery(CurrentUserId, eventId), ct)
        );
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
        [FromServices]
            IQueryHandler<
            ListActivityRoleTypesQuery,
            IReadOnlyList<ActivityRoleTypeResponse>
        > handler,
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
        [FromServices]
            IQueryHandler<
            GetHouseholdSignupRolesQuery,
            IReadOnlyList<HouseholdSignupRolesResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetHouseholdSignupRolesQuery(CurrentUserId), ct));
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
        [FromServices]
            IQueryHandler<
            ListAssignmentStatusTypesQuery,
            IReadOnlyList<AssignmentStatusTypeResponse>
        > handler,
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
        [FromServices]
            IQueryHandler<
            ListActivityModalityTypesQuery,
            IReadOnlyList<ActivityModalityTypeResponse>
        > handler,
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
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ActivityResponse>> CreateAsync(
        Guid eventId,
        [FromBody] CreateActivityRequest request,
        [FromServices] ICommandHandler<CreateActivityCommand, Result<ActivityId>> handler,
        [FromServices] IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(EventId.From(eventId)), ct),
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
        [FromServices] ICommandHandler<UpdateActivityCommand, Result> handler,
        [FromServices] IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(ActivityId.From(activityId)), ct),
            () => getById.HandleAsync(new GetActivityByIdQuery(ActivityId.From(activityId)), ct)
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
        [FromServices] ICommandHandler<DeleteActivityCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteActivityCommand(ActivityId.From(activityId)), ct)
        );
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
        [FromServices] ICommandHandler<AssignActivityCommand, Result> handler,
        [FromServices] IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>> getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                request.ToCommand(ActivityId.From(activityId), UserId.From(userId)),
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
        [FromServices]
            ICommandHandler<AssignHouseholdCommand, Result<IReadOnlyList<UserId>>> handler,
        [FromServices]
            IQueryHandler<GetAssignmentsQuery, IReadOnlyList<AssignmentResponse>> getAssignments,
        CancellationToken ct
    )
    {
        return await ToOkListAfterAsync(
            await handler.HandleAsync(request.ToCommand(ActivityId.From(activityId)), ct),
            userIds =>
                getAssignments.HandleAsync(
                    new GetAssignmentsQuery(activityId, [.. userIds.Select(id => id.Value)]),
                    ct
                )
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
        [FromServices] ICommandHandler<UnassignActivityCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new UnassignActivityCommand(ActivityId.From(activityId), UserId.From(userId)),
                ct
            )
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
        [FromServices] ICommandHandler<ChangeAssignmentStatusCommand, Result> handler,
        [FromServices] IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>> getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new ChangeAssignmentStatusCommand(
                    ActivityId.From(activityId),
                    UserId.From(userId),
                    request.AssignmentStatusId
                ),
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
        [FromServices] ICommandHandler<ChangeAssignmentRoleCommand, Result> handler,
        [FromServices] IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>> getAssignment,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new ChangeAssignmentRoleCommand(
                    ActivityId.From(activityId),
                    UserId.From(userId),
                    request.ActivityRoleTypeId
                ),
                ct
            ),
            () => getAssignment.HandleAsync(new GetAssignmentQuery(activityId, userId), ct)
        );
    }
}
