using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<Result<UserResponse>>;

public sealed class GetUserByIdQueryHandler(IUserRepository users, IQueryExecutor executor)
    : IQueryHandler<GetUserByIdQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        GetUserByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == query.UserId).Select(Projections.UserWithType),
            ct
        );
        return response is null
            ? (Result<UserResponse>)Error.NotFound(ErrorCode.UserNotFound)
            : (Result<UserResponse>)response;
    }
}
