using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

/// <summary>
/// All file reads. Metadata is a normal OData entity read (<c>Files({key})</c>); the raw content
/// download stays a plain binary GET (it returns a stream, not queryable data) but lives here so
/// every read sits on an OData controller. File mutations are on <c>FileCommandsController</c>.
/// </summary>
[AllowAnonymous]
public class FilesController(IFileService files) : ODataController
{
    public async Task<ActionResult<FileResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await files.GetByIdAsync(key, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet("api/files/{fileId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid fileId, CancellationToken ct)
    {
        var result = await files.GetContentAsync(fileId, ct);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var content = result.Value;
        return File(content.Content, content.ContentType);
    }
}
