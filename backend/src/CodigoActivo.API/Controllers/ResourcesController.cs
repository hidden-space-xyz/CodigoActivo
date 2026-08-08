using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<PagedResult<ResourceListItemResponse>>> ListAsync(
        [FromQuery] ResourceListQuery query,
        [FromServices] ListResourcesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListResourcesQuery(query), ct));
    }

    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ResourceTypeResponse>>> TypesAsync(
        [FromServices] ListResourceTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListResourceTypesQuery(), ct));
    }

    [HttpGet("{resourceId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<ResourceResponse>> GetAsync(
        Guid resourceId,
        [FromServices] GetResourceByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetResourceByIdQuery(resourceId), ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> CreateAsync(
        [FromBody] CreateResourceRequest request,
        [FromServices] CreateResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateResourceCommand(request, UserId), ct),
            r => $"/api/resources/{r.Id}"
        );
    }

    [HttpPut("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> UpdateAsync(
        Guid resourceId,
        [FromBody] UpdateResourceRequest request,
        [FromServices] UpdateResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateResourceCommand(resourceId, request, UserId), ct)
        );
    }

    [HttpDelete("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid resourceId,
        [FromServices] DeleteResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteResourceCommand(resourceId), ct));
    }
}
