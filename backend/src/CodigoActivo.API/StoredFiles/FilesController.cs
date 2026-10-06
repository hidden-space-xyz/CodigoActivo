using System.Globalization;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Application.Files.Contracts;
using CodigoActivo.Application.Files.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.StoredFiles;

/// <summary>
/// Exposes HTTP endpoints for querying and managing files.
/// </summary>
[ApiController]
[Route("api/files")]
public class FilesController : ApiControllerBase
{
    /// <summary>
    /// Gets the requested file.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a file, or an error response.</returns>
    [HttpGet("{fileId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<ActionResult<FileResponse>> GetAsync(
        Guid fileId,
        [FromServices] IQueryHandler<GetFileByIdQuery, Result<FileResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetFileByIdQuery(StoredFileId.From(fileId)), ct));
    }

    /// <summary>
    /// Gets the requested content.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpGet("{fileId:guid}/content")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Files)]
    public async Task<IActionResult> GetContentAsync(
        Guid fileId,
        [FromServices] IQueryHandler<GetFileContentQuery, Result<FileContent>> handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(
            new GetFileContentQuery(StoredFileId.From(fileId)),
            ct
        );
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

    /// <summary>
    /// Creates a file from the validated request.
    /// </summary>
    /// <param name="file">The file value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a file, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    [EnableRateLimiting(SecurityPolicies.FileUploads)]
    [ProducesResponseType<FileResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FileResponse>> CreateAsync(
        IFormFile? file,
        [FromServices] ICommandHandler<CreateFileCommand, Result<StoredFileId>> handler,
        [FromServices] IQueryHandler<GetFileByIdQuery, Result<FileResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(new CreateFileCommand(ToUploadRequest(file)), ct),
            id => getById.HandleAsync(new GetFileByIdQuery(id), ct),
            id => $"/api/files/{id}"
        );
    }

    /// <summary>
    /// Updates the selected file with the validated request.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="file">The file value.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a file, or an error response.</returns>
    [HttpPut("{fileId:guid}")]
    [AllowOnlyAdmin]
    [Consumes("multipart/form-data")]
    [FileUploadSizeLimit]
    [EnableRateLimiting(SecurityPolicies.FileUploads)]
    public async Task<ActionResult<FileResponse>> UpdateAsync(
        Guid fileId,
        IFormFile? file,
        [FromServices] ICommandHandler<UpdateFileCommand, Result> handler,
        [FromServices] IQueryHandler<GetFileByIdQuery, Result<FileResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new UpdateFileCommand(StoredFileId.From(fileId), ToUploadRequest(file)),
                ct
            ),
            () => getById.HandleAsync(new GetFileByIdQuery(StoredFileId.From(fileId)), ct)
        );
    }

    /// <summary>
    /// Deletes the selected file.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{fileId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid fileId,
        [FromServices] ICommandHandler<DeleteFileCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteFileCommand(StoredFileId.From(fileId)), ct)
        );
    }

    private static FileUpload? ToUploadRequest(IFormFile? file)
    {
        return file is null
            ? null
            : new FileUpload(file.OpenReadStream(), file.FileName, file.Length);
    }
}
