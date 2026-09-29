using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Files.Contracts;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class FileProjections
{
    /// <summary>
    /// Stores the shared file value.
    /// </summary>
    public static readonly Expression<Func<FileRow, FileResponse>> File = file => new FileResponse(
        file.Id,
        file.Name,
        file.Extension,
        file.UploadedAt,
        file.UploadedBy
    );
}
