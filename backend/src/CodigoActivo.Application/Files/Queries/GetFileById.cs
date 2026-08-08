using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Files.Queries;

public sealed record GetFileByIdQuery(Guid FileId) : IQuery<Result<FileResponse>>;

public sealed class GetFileByIdQueryHandler(IFileRepository files)
    : IQueryHandler<GetFileByIdQuery, Result<FileResponse>>
{
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
