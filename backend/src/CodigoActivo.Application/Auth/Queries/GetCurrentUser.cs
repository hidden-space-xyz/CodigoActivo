using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Queries;

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<Result<UserResponse>>;

public sealed class GetCurrentUserQueryHandler(IUserRepository users)
    : IQueryHandler<GetCurrentUserQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdWithDetailsAsync(query.UserId, ct);
        return user is null
            ? (Result<UserResponse>)Error.Unauthorized(ErrorCode.CurrentUserNotFound)
            : (Result<UserResponse>)user.ToResponse();
    }
}
