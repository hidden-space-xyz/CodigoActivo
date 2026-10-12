using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the request for the account of the signed-in user.
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<Result<UserResponse>>;

/// <summary>
/// Executes the query to retrieve the account of the signed-in user.
/// </summary>
/// <param name="currentUser">Person the query runs for.</param>
/// <param name="accountQuery">Query that reads an account as its owner sees it.</param>
public sealed class GetCurrentUserQueryHandler(
    ICurrentUser currentUser,
    IQueryHandler<GetAccountQuery, Result<UserResponse>> accountQuery
) : IQueryHandler<GetCurrentUserQuery, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to retrieve current user.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
    public Task<Result<UserResponse>> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken ct = default
    )
    {
        return accountQuery.HandleAsync(new GetAccountQuery(currentUser.RequiredId()), ct);
    }
}
