using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(IActivityService activities, IParticipationService participation)
    : ApiControllerBase
{
    [HttpGet("assigned-activities")]
    public async Task<ActionResult<IReadOnlyList<AssignedActivityResponse>>> AssignedActivities(
        [FromQuery] Guid? eventId,
        CancellationToken ct
    )
    {
        return Ok(await activities.ListAssignedAsync(UserId, eventId, ct));
    }

    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistory(
        CancellationToken ct
    )
    {
        return Ok(await participation.GetHistoryAsync(UserId, ct));
    }
}
