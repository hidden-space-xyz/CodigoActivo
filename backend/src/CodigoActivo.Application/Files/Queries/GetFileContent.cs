using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Queries;

/// <summary>
/// Carries the criteria used to retrieve file content.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record GetFileContentQuery(Guid FileId) : IQuery<Result<FileContent>>;

/// <summary>
/// Executes the query to retrieve file content.
/// </summary>
/// <param name="getById">Handler used to retrieve file by identifier.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
public sealed class GetFileContentQueryHandler(
    GetFileByIdQueryHandler getById,
    ILocalFileSystemRepository storage
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
            FileNaming.StoredName(meta.Value.Id, meta.Value.Extension),
            ct
        );
        if (stream is null)
        {
            return Error.NotFound(ErrorCode.FileContentMissingFromStorage);
        }

        var format = await stream.DetectImageFormatAsync(ct);
        stream.Position = 0;

        return new FileContent(
            stream,
            format?.ContentType ?? FallbackContentType,
            meta.Value.Name,
            meta.Value.UploadedAt
        );
    }
}
