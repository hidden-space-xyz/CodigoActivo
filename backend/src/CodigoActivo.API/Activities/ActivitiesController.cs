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
/// Exposes HTTP endpoints for querying and managing the activities of the events, the signups to
/// them and their catalogs.
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
    /// <returns>An HTTP response containing a page of activities.</returns>
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
    /// Lists the other activities the person is signed up to, except denied signups, whose times
    /// overlap the selected activity. It answers for any person the signed-in user may act for.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person whose signups are checked.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the overlapping activities, or an error response.</returns>
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
    /// Lists the signups of the signed-in user and their dependents to the activities of an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the household signups, or an error response.</returns>
    [HttpGet("household-assignments/{eventId:guid}")]
    [Authorize]
    public async Task<
        ActionResult<IReadOnlyList<HouseholdMemberAssignmentResponse>>
    > HouseholdAssignmentsAsync(
        Guid eventId,
        [FromServices]
            IQueryHandler<
            GetHouseholdAssignmentsQuery,
            Result<IReadOnlyList<HouseholdMemberAssignmentResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new GetHouseholdAssignmentsQuery(EventId.From(eventId)), ct)
        );
    }

    /// <summary>
    /// Lists the catalog of activity roles.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the activity roles.</returns>
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
    /// Lists, for the signed-in user and each of their dependents, the roles they may sign up with.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signup roles of each household member.</returns>
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
        return Ok(await handler.HandleAsync(new GetHouseholdSignupRolesQuery(), ct));
    }

    /// <summary>
    /// Lists the catalog of signup statuses.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signup statuses.</returns>
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
    /// Lists the catalog of activity modalities.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the activity modalities.</returns>
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
    /// Creates an activity of the event from the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event the activity belongs to.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the created activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the created activity and its location, or an error response.</returns>
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
    /// Deletes the selected activity with its signups, keeping their data history.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
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
    /// Signs the person up to the activity with the requested role, pending a decision. The
    /// signed-in user may sign up themselves or a dependent; an administrator may sign up anyone,
    /// even outside the signup window.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person signed up.</param>
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
            () =>
                getAssignment.HandleAsync(
                    new GetAssignmentQuery(ActivityId.From(activityId), UserId.From(userId)),
                    ct
                )
        );
    }

    /// <summary>
    /// Signs members of the signed-in user's household up to the activity; members already signed
    /// up are left as they are.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getAssignments">Query handler that reads the created assignments.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signups made now, or an error response.</returns>
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
                    new GetAssignmentsQuery(ActivityId.From(activityId), userIds),
                    ct
                )
        );
    }

    /// <summary>
    /// Withdraws the person from the activity. The signed-in user may withdraw themselves or a
    /// dependent while the signup is open; an administrator may withdraw anyone at any time.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person withdrawn.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
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
    /// Changes the status of the person's signup to the activity; the person is told about a
    /// decision once it is committed.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person signed up.</param>
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
                request.ToCommand(ActivityId.From(activityId), UserId.From(userId)),
                ct
            ),
            () =>
                getAssignment.HandleAsync(
                    new GetAssignmentQuery(ActivityId.From(activityId), UserId.From(userId)),
                    ct
                )
        );
    }

    /// <summary>
    /// Changes the role of the person's signup to the activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person signed up.</param>
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
                request.ToCommand(ActivityId.From(activityId), UserId.From(userId)),
                ct
            ),
            () =>
                getAssignment.HandleAsync(
                    new GetAssignmentQuery(ActivityId.From(activityId), UserId.From(userId)),
                    ct
                )
        );
    }
}
