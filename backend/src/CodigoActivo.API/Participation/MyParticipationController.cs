using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Application.Participation.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Participation;

/// <summary>
/// Exposes HTTP endpoints for the events the signed-in user took part in.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyParticipationController : ApiControllerBase
{
    /// <summary>
    /// Executes the event history endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event history, or an error response.</returns>
    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistoryAsync(
        [FromServices]
            IQueryHandler<GetEventHistoryQuery, IReadOnlyList<EventHistoryResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventHistoryQuery(CurrentUserId.Value), ct));
    }

    /// <summary>
    /// Executes the certificates endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event certificate, or an error response.</returns>
    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyList<EventCertificateResponse>>> CertificatesAsync(
        [FromServices]
            IQueryHandler<
            GetEventCertificatesQuery,
            IReadOnlyList<EventCertificateResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(
            await handler.HandleAsync(new GetEventCertificatesQuery(CurrentUserId.Value), ct)
        );
    }
}
