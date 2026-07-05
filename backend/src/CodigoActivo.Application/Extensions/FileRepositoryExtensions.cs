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
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct)) return Error.BadRequest(missingCode);

        return Result.Success();
    }
}
