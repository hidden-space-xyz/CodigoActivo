using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

/// <summary>Reads scoped to the current authenticated user (identity taken from the session cookie).</summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(IActivityService activities) : ApiControllerBase
{
    [HttpGet("assigned-activities")]
    public async Task<ActionResult<IReadOnlyList<AssignedActivityResponse>>> AssignedActivities(
        CancellationToken ct
    )
    {
        return Ok(await activities.ListAssignedAsync(UserId, ct));
    }
}
