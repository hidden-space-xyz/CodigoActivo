using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
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
        if (file is null) return ToProblem(Error.BadRequest(ErrorCode.FileUploadMissing));

        if (file.Length == 0) return ToProblem(Error.BadRequest(ErrorCode.FileUploadEmpty));

        var upload = new FileUploadRequest(file.OpenReadStream(), file.FileName, file.Length);
        var result = await files.CreateAsync(upload, UserId, ct);
        return result.IsFailure
            ? (ActionResult<FileResponse>)ToProblem(result.Error!)
            : (ActionResult<FileResponse>)Created($"/api/files/{result.Value.Id}", result.Value);
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
        if (file is null) return ToProblem(Error.BadRequest(ErrorCode.FileUploadMissing));

        if (file.Length == 0) return ToProblem(Error.BadRequest(ErrorCode.FileUploadEmpty));

        var upload = new FileUploadRequest(file.OpenReadStream(), file.FileName, file.Length);
        return ToOk(await files.UpdateAsync(fileId, upload, ct));
    }

    [HttpDelete("{fileId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken ct)
    {
        return ToNoContent(await files.DeleteAsync(fileId, ct));
    }
}
