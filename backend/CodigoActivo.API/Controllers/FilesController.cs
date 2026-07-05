using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(IFileService files) : ApiControllerBase
{
    // The multipart envelope (boundary lines + part headers) adds a little on top of the raw file
    // bytes, so the request-body cap needs headroom above the storage limit — otherwise a file of
    // exactly MaxSizeBytes would be rejected by Kestrel with a 413 before the storage-size guard
    // (which is the single source of truth) could accept it.
    private const long MaxRequestBodyBytes = FileStorageOptions.DefaultMaxSizeBytes + (64 * 1024);

    [HttpGet("{fileId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<FileResponse>> Get(Guid fileId, CancellationToken ct)
    {
        return ToOk(await files.GetByIdAsync(fileId, ct));
    }

    [HttpGet("{fileId:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContent(Guid fileId, CancellationToken ct)
    {
        var result = await files.GetContentAsync(fileId, ct);
        if (result.IsFailure) return ToProblem(result.Error!);

        var content = result.Value;

        // Image content is immutable for a given (id, uploaded-at); let browsers cache it and
        // revalidate with a conditional GET so repeat page views are 304s instead of full
        // re-downloads. The File overload handles If-None-Match / If-Modified-Since automatically.
        var lastModified = content.UploadedAt;
        var etag = new EntityTagHeaderValue($"\"{fileId:N}-{lastModified.UtcTicks}\"");
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            MaxAge = TimeSpan.FromMinutes(5),
        };
        return File(content.Content, content.ContentType, lastModified, etag);
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<ActionResult<FileResponse>> Create(IFormFile? file, CancellationToken ct)
    {
        return ToCreated(await files.CreateAsync(ToUploadRequest(file), UserId, ct), f => $"/api/files/{f.Id}");
    }

    [HttpPut("{fileId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
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
        return file is null ? null : new FileUploadRequest(file.OpenReadStream(), file.FileName, file.Length);
    }
}
