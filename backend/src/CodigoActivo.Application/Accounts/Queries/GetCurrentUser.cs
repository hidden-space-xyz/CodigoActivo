using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Users;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the criteria used to retrieve current user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<Result<UserResponse>>;

/// <summary>
/// Executes the query to retrieve current user.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetCurrentUserQueryHandler(IReadStore readStore, IQueryExecutor executor)
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
        var response = await executor.FirstOrDefaultAsync(
            readStore.Users.Where(u => u.Id == query.UserId).Select(UserProjections.User),
            ct
        );
        return response is null ? Error.Unauthorized(ErrorCode.CurrentUserNotFound) : response;
    }
}
