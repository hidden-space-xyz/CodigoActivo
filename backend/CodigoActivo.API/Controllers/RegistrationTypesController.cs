using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

/// <summary>Public catalog of the user types offered on the registration form.</summary>
[ApiController]
[Route("api/registration-types")]
public class RegistrationTypesController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RegistrationTypeResponse>>> List(
        [FromQuery] RegistrationAudience? audience,
        CancellationToken ct
    )
    {
        return Ok(await users.ListRegistrationTypesAsync(audience, ct));
    }
}
