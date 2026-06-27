using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/partners")]
public class PartnersController(IPartnerService partners) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Cached(nameof(Partner))]
    public async Task<ActionResult<IReadOnlyList<PartnerResponse>>> List(CancellationToken ct)
    {
        return Ok(await partners.ListAsync(ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Partner), nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<PartnerResponse>> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken ct
    )
    {
        var result = await partners.CreateAsync(request, UserId, ct);
        return result.IsFailure
            ? (ActionResult<PartnerResponse>)ToProblem(result.Error!)
            : (ActionResult<PartnerResponse>)CreatedAtAction(nameof(List), null, result.Value);
    }

    [HttpPut("{partnerId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Partner))]
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
    [InvalidatesCache(nameof(Partner), nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid partnerId, CancellationToken ct)
    {
        return ToNoContent(await partners.DeleteAsync(partnerId, ct));
    }
}
