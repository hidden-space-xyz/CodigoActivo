using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Files.Queries;

/// <summary>
/// Carries the criteria used to retrieve file by identifier.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record GetFileByIdQuery(Guid FileId) : IQuery<Result<FileResponse>>;

/// <summary>
/// Executes the query to retrieve file by identifier.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
public sealed class GetFileByIdQueryHandler(IFileRepository files)
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
        var matches = await files.GetAsync(f => f.Id == query.FileId, ct);
        var response = matches.Count is 0 ? null : matches[0].ToResponse();

        return response is null ? Error.NotFound(ErrorCode.FileNotFound) : Result.Success(response);
    }
}
