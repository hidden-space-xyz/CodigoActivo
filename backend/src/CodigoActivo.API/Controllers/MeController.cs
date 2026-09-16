using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Participation.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing me.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ApiControllerBase
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
        [FromServices] ListAssignedActivitiesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListAssignedActivitiesQuery(UserId, eventId), ct));
    }

    /// <summary>
    /// Executes the event history endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event history, or an error response.</returns>
    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistoryAsync(
        [FromServices] GetEventHistoryQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventHistoryQuery(UserId), ct));
    }

    /// <summary>
    /// Executes the certificates endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event certificate, or an error response.</returns>
    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyList<EventCertificateResponse>>> CertificatesAsync(
        [FromServices] GetEventCertificatesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventCertificatesQuery(UserId), ct));
    }
}
