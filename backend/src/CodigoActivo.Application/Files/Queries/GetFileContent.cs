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
/// Executes the query to retrieve file content.
/// </summary>
/// <param name="getById">Handler used to retrieve file by identifier.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
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
    /// <returns>A task whose result contains a file content on success, or an application error on failure.</returns>
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
