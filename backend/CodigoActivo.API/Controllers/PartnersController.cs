using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/partners")]
public class PartnersController(IPartnerService partners) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<PartnerResponse>>> List(
        [FromQuery] PartnerListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await partners.ListAsync(query, ct));
    }

    [HttpGet("{partnerId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PartnerResponse>> Get(Guid partnerId, CancellationToken ct)
    {
        return ToOk(await partners.GetByIdAsync(partnerId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken ct
    )
    {
        var result = await partners.CreateAsync(request, UserId, ct);
        if (result.IsFailure) return ToProblem(result.Error!);

        return Created($"/api/partners/{result.Value.Id}", result.Value);
    }

    [HttpPut("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> Update(
        Guid partnerId,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await partners.UpdateAsync(partnerId, request, UserId, ct));
    }

    [HttpDelete("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid partnerId, CancellationToken ct)
    {
        return ToNoContent(await partners.DeleteAsync(partnerId, ct));
    }
}
