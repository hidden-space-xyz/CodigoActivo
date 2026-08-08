using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/partners")]
public class PartnersController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Partners)]
    public async Task<ActionResult<PagedResult<PartnerResponse>>> ListAsync(
        [FromQuery] PartnerListQuery query,
        [FromServices] ListPartnersQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListPartnersQuery(query), ct));
    }

    [HttpGet("{partnerId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Partners)]
    public async Task<ActionResult<PartnerResponse>> GetAsync(
        Guid partnerId,
        [FromServices] GetPartnerByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetPartnerByIdQuery(partnerId), ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> CreateAsync(
        [FromBody] CreatePartnerRequest request,
        [FromServices] CreatePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreatePartnerCommand(request, UserId), ct),
            p => $"/api/partners/{p.Id}"
        );
    }

    [HttpPut("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> UpdateAsync(
        Guid partnerId,
        [FromBody] UpdatePartnerRequest request,
        [FromServices] UpdatePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdatePartnerCommand(partnerId, request, UserId), ct)
        );
    }

    [HttpDelete("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid partnerId,
        [FromServices] DeletePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeletePartnerCommand(partnerId), ct));
    }
}
