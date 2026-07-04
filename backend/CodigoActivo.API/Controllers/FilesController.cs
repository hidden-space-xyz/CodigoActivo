using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(IFileService files) : ApiControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

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
        return File(content.Content, content.ContentType);
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<FileResponse>> Create(IFormFile? file, CancellationToken ct)
    {
        return ToCreated(await files.CreateAsync(ToUploadRequest(file), UserId, ct), f => $"/api/files/{f.Id}");
    }

    [HttpPut("{fileId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
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
