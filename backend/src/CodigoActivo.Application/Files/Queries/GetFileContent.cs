using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Queries;

public sealed record GetFileContentQuery(Guid FileId) : IQuery<Result<FileContent>>;

public sealed class GetFileContentQueryHandler(
    GetFileByIdQueryHandler getById,
    ILocalFileSystemRepository storage
) : IQueryHandler<GetFileContentQuery, Result<FileContent>>
{
    private const string FallbackContentType = "application/octet-stream";

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
