using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourceCommandsController(IResourceService resources) : ApiControllerBase
{
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken ct
    )
    {
        var result = await resources.CreateAsync(request, UserId, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        return Created($"/api/odata/Resources({result.Value.Id})", result.Value);
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
