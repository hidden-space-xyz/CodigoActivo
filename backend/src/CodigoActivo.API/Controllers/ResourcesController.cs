using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController(IResourceService resources) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<PagedResult<ResourceListItemResponse>>> ListAsync(
        [FromQuery] ResourceListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await resources.ListAsync(query, ct));
    }

    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ResourceTypeResponse>>> TypesAsync(
        CancellationToken ct
    )
    {
        return Ok(await resources.ListTypesAsync(ct));
    }

    [HttpGet("{resourceId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<ResourceResponse>> GetAsync(
        Guid resourceId,
        CancellationToken ct
    )
    {
        return ToOk(await resources.GetByIdAsync(resourceId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> CreateAsync(
        [FromBody] CreateResourceRequest request,
        CancellationToken ct
    )
    {
        return ToCreated(
            await resources.CreateAsync(request, UserId, ct),
            r => $"/api/resources/{r.Id}"
        );
    }

    [HttpPut("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> UpdateAsync(
        Guid resourceId,
        [FromBody] UpdateResourceRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await resources.UpdateAsync(resourceId, request, UserId, ct));
    }

    [HttpDelete("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(Guid resourceId, CancellationToken ct)
    {
        return ToNoContent(await resources.DeleteAsync(resourceId, ct));
    }
}
