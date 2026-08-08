using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Participation.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ApiControllerBase
{
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

    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistoryAsync(
        [FromServices] GetEventHistoryQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventHistoryQuery(UserId), ct));
    }

    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyList<EventCertificateResponse>>> CertificatesAsync(
        [FromServices] GetEventCertificatesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventCertificatesQuery(UserId), ct));
    }
}
