using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/partners")]
public class PartnerCommandsController(IPartnerService partners) : CommandControllerBase
{
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken ct
    )
    {
        var result = await partners.CreateAsync(request, UserId, ct);
        if (result.IsFailure) return ToProblem(result.Error!);

        return Created($"/api/odata/Partners({result.Value.Id})", result.Value);
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