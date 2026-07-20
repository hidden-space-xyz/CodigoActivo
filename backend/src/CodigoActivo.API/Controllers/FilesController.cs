using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(IFileService files) : ApiControllerBase
{
    [HttpGet("{fileId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<ActionResult<FileResponse>> Get(Guid fileId, CancellationToken ct)
    {
        return ToOk(await files.GetByIdAsync(fileId, ct));
    }

    [HttpGet("{fileId:guid}/content")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<IActionResult> GetContent(Guid fileId, CancellationToken ct)
    {
        var result = await files.GetContentAsync(fileId, ct);
        if (result.IsFailure)
            return ToProblem(result.Error!);

        var content = result.Value;

        var lastModified = content.UploadedAt;
        var etag = new EntityTagHeaderValue($"\"{fileId:N}-{lastModified.UtcTicks}\"");
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
    public async Task<ActionResult<FileResponse>> Create(IFormFile? file, CancellationToken ct)
    {
        return ToCreated(
            await files.CreateAsync(ToUploadRequest(file), UserId, ct),
            f => $"/api/files/{f.Id}"
        );
    }

    [HttpPut("{fileId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    public async Task<ActionResult<FileResponse>> Update(
        Guid fileId,
        IFormFile? file,
        CancellationToken ct
    )
    {
        return ToOk(await files.UpdateAsync(fileId, ToUploadRequest(file), ct));
    }

    [HttpDelete("{fileId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken ct)
    {
        return ToNoContent(await files.DeleteAsync(fileId, ct));
    }

    private static FileUploadRequest? ToUploadRequest(IFormFile? file)
    {
        return file is null
            ? null
            : new FileUploadRequest(file.OpenReadStream(), file.FileName, file.Length);
    }
}
