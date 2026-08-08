using System.Globalization;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Application.Files.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ApiControllerBase
{
    [HttpGet("{fileId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<ActionResult<FileResponse>> GetAsync(
        Guid fileId,
        [FromServices] GetFileByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetFileByIdQuery(fileId), ct));
    }

    [HttpGet("{fileId:guid}/content")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<IActionResult> GetContentAsync(
        Guid fileId,
        [FromServices] GetFileContentQueryHandler handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(new GetFileContentQuery(fileId), ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var content = result.Value;

        var lastModified = content.UploadedAt;
        var ticks = lastModified.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var etag = new EntityTagHeaderValue($"\"{fileId:N}-{ticks}\"");
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            NoCache = true,
        };
        return File(content.Content, content.ContentType, lastModified, etag);
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<FileResponse>> CreateAsync(
        IFormFile? file,
        [FromServices] CreateFileCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateFileCommand(ToUploadRequest(file), UserId), ct),
            f => $"/api/files/{f.Id}"
        );
    }

    [HttpPut("{fileId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<FileResponse>> UpdateAsync(
        Guid fileId,
        IFormFile? file,
        [FromServices] UpdateFileCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateFileCommand(fileId, ToUploadRequest(file)), ct)
        );
    }

    [HttpDelete("{fileId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid fileId,
        [FromServices] DeleteFileCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteFileCommand(fileId), ct));
    }

    private static FileUpload? ToUploadRequest(IFormFile? file)
    {
        return file is null
            ? null
            : new FileUpload(file.OpenReadStream(), file.FileName, file.Length);
    }
}
