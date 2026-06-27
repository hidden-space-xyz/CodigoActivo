using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController(IResourceService resources) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Cached(nameof(Resource))]
    public async Task<ActionResult<IReadOnlyList<ResourceResponse>>> List(CancellationToken ct)
    {
        return Ok(await resources.ListAsync(ct));
    }

    [HttpGet("{resourceId:guid}")]
    [AllowAnonymous]
    [Cached(nameof(Resource))]
    public async Task<ActionResult<ResourceResponse>> GetById(Guid resourceId, CancellationToken ct)
    {
        return ToOk(await resources.GetByIdAsync(resourceId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Resource), nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<ResourceResponse>> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken ct
    )
    {
        var result = await resources.CreateAsync(request, UserId, ct);
        return result.IsFailure
            ? (ActionResult<ResourceResponse>)ToProblem(result.Error!)
            : (ActionResult<ResourceResponse>)
                CreatedAtAction(
                    nameof(GetById),
                    new { resourceId = result.Value.Id },
                    result.Value
                );
    }

    [HttpPut("{resourceId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Resource))]
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
    [InvalidatesCache(nameof(Resource), nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid resourceId, CancellationToken ct)
    {
        return ToNoContent(await resources.DeleteAsync(resourceId, ct));
    }
}
