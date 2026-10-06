using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files.Queries;

/// <summary>
/// Carries the criteria used to retrieve file by identifier.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record GetFileByIdQuery(StoredFileId FileId) : IQuery<Result<FileResponse>>;

/// <summary>
/// Executes the query to retrieve file by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetFileByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetFileByIdQuery, Result<FileResponse>>
{
    /// <summary>
    /// Handles the request to retrieve file by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a file on success, or an application error on failure.</returns>
    public async Task<Result<FileResponse>> HandleAsync(
        GetFileByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore.Files.Where(f => f.Id == query.FileId.Value).Select(FileProjections.File),
            ct
        );
        return response is null ? Error.NotFound(ApplicationErrorCode.FileNotFound) : response;
    }
}
