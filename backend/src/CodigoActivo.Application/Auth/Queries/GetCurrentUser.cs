using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Queries;

/// <summary>
/// Carries the criteria used to retrieve current user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<Result<UserResponse>>;

/// <summary>
/// Executes the query to retrieve current user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
public sealed class GetCurrentUserQueryHandler(IUserRepository users)
    : IQueryHandler<GetCurrentUserQuery, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to retrieve current user.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
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
