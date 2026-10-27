using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files.Queries;

/// <summary>
/// Carries the criteria used to retrieve file content.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
/// <param name="Variant">Variant of the file to read.</param>
public sealed record GetFileContentQuery(StoredFileId FileId, FileVariant Variant)
    : IQuery<Result<FileContent>>;

/// <summary>
/// Executes the query to open the stored content of one variant of a file.
/// </summary>
/// <param name="getById">Handler that reads the metadata of the file.</param>
/// <param name="storage">Storage holding the content of the files.</param>
public sealed class GetFileContentQueryHandler(
    IQueryHandler<GetFileByIdQuery, Result<FileResponse>> getById,
    IFileStorage storage
) : IQueryHandler<GetFileContentQuery, Result<FileContent>>
{
    private const string FallbackContentType = "application/octet-stream";

    /// <summary>
    /// Handles the request to retrieve file content.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the content; <see cref="ApplicationErrorCode.FileNotFound"/>
    /// when the file is unknown, or
    /// <see cref="ApplicationErrorCode.FileContentMissingFromStorage"/> when its row exists but the
    /// variant is not in storage.
    /// </returns>
    public async Task<Result<FileContent>> HandleAsync(
        GetFileContentQuery query,
        CancellationToken ct = default
    )
    {
        var meta = await getById.HandleAsync(new GetFileByIdQuery(query.FileId), ct);
        if (meta.IsFailure)
        {
            return meta.Error!;
        }

        var stream = await storage.OpenReadAsync(
            FileNaming.StoredName(
                StoredFileId.From(meta.Value.Id),
                meta.Value.Extension,
                query.Variant
            ),
            ct
        );
        if (stream is null)
        {
            return Error.NotFound(ApplicationErrorCode.FileContentMissingFromStorage);
        }

        return new FileContent(
            stream,
            ImageFormat.FromExtension(meta.Value.Extension)?.ContentType ?? FallbackContentType,
            meta.Value.Name,
            meta.Value.UploadedAt
        );
    }
}
