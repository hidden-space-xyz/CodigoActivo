using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class FilesController(IFileService files) : ODataController
{
    public async Task<ActionResult<FileResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await files.GetByIdAsync(key, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("api/files/{fileId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid fileId, CancellationToken ct)
    {
        var result = await files.GetContentAsync(fileId, ct);
        if (result.IsFailure)
        {
            return this.ToProblemResult(result.Error!);
        }

        var content = result.Value;
        return File(content.Content, content.ContentType);
    }
}
