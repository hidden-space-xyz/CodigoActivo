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
[Route("api/resources")]
public class ResourcesController(IResourceService resources) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ResourceListItemResponse>>> List(
        [FromQuery] ResourceListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await resources.ListAsync(query, ct));
    }

    [HttpGet("{resourceId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ResourceResponse>> Get(Guid resourceId, CancellationToken ct)
    {
        return ToOk(await resources.GetByIdAsync(resourceId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> Create(
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
    public async Task<ActionResult<ResourceResponse>> Update(
        Guid resourceId,
        [FromBody] UpdateResourceRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await resources.UpdateAsync(resourceId, request, UserId, ct));
    }

    [HttpDelete("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid resourceId, CancellationToken ct)
    {
        return ToNoContent(await resources.DeleteAsync(resourceId, ct));
    }
}
