using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Activities;

/// <summary>
/// Exposes HTTP endpoints for the activities the signed-in user is signed up to.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyActivitiesController : ApiControllerBase
{
    /// <summary>
    /// Lists the signups of the signed-in user, whatever their status and ordered by the start of
    /// the activity; those of their dependents are not included.
    /// </summary>
    /// <param name="eventId">
    /// Identifier of the event whose activities are listed, or null for every event.
    /// </param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signups of the signed-in user.</returns>
    [HttpGet("assigned-activities")]
    public async Task<
        ActionResult<IReadOnlyList<AssignedActivityResponse>>
    > AssignedActivitiesAsync(
        [FromQuery] Guid? eventId,
        [FromServices]
            IQueryHandler<
            ListAssignedActivitiesQuery,
            IReadOnlyList<AssignedActivityResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(
            await handler.HandleAsync(
                new ListAssignedActivitiesQuery(
                    eventId is { } filter ? EventId.From(filter) : null
                ),
                ct
            )
        );
    }
}
