using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Activities;

/// <summary>
/// Exposes HTTP endpoints for the activities the signed-in user and their household are signed up to.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyActivitiesController : ApiControllerBase
{
    /// <summary>
    /// Assigns ed activities according to the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assigned activity response>>, or an error response.</returns>
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
            await handler.HandleAsync(new ListAssignedActivitiesQuery(CurrentUserId, eventId), ct)
        );
    }
}
