using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Extensions;

public static class FileRepositoryExtensions
{
    public static async Task<Result> EnsureThumbnailExistsAsync(
        this IFileRepository files,
        Guid thumbnailId,
        ErrorCode missingCode,
        CancellationToken ct
    )
    {
        return !await files.ExistsAsync(f => f.Id == thumbnailId, ct)
            ? (Result)Error.BadRequest(missingCode)
            : Result.Success();
    }
}
