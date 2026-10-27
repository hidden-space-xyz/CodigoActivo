using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Application.TermsDecisions.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Participation;

/// <summary>
/// Exposes HTTP endpoints for the participation of the signed-in user and their dependents: the
/// events they signed up to, their attendance certificates and their consents.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyParticipationController : ApiControllerBase
{
    /// <summary>
    /// Lists the events the signed-in user or their dependents signed up to; a past event shows only
    /// the confirmed signups and is left out when it has none.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the event history.</returns>
    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistoryAsync(
        [FromServices]
            IQueryHandler<GetEventHistoryQuery, IReadOnlyList<EventHistoryResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventHistoryQuery(), ct));
    }

    /// <summary>
    /// Lists the attendance certificates of the signed-in user and their dependents: one per person
    /// and past event in which the person had a confirmed signup.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the certificates.</returns>
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
        return Ok(await handler.HandleAsync(new GetEventCertificatesQuery(), ct));
    }

    /// <summary>
    /// Lists the consents decided for the signed-in user and their dependents, so the ones given
    /// can be withdrawn.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the consents.</returns>
    [HttpGet("consents")]
    public async Task<ActionResult<IReadOnlyList<MyConsentResponse>>> ConsentsAsync(
        [FromServices] IQueryHandler<ListMyConsentsQuery, IReadOnlyList<MyConsentResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListMyConsentsQuery(), ct));
    }
}
