using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the account a request has just verified or signed in, before the caller's session
/// names it.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record GetAccountQuery(UserId UserId) : IQuery<Result<UserResponse>>;

/// <summary>
/// Executes the query to retrieve an account as its owner sees it.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetAccountQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetAccountQuery, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the account.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
    public async Task<Result<UserResponse>> HandleAsync(
        GetAccountQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var response = await executor.FirstOrDefaultAsync(
            readStore.Users.Where(u => u.Id == query.UserId.Value).Select(UserProjections.User),
            ct
        );
        return response is null
            ? Error.Unauthorized(ApplicationErrorCode.CurrentUserNotFound)
            : response;
    }
}
