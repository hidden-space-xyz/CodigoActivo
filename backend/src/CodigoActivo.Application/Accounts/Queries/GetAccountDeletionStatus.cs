using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the criteria used to tell whether the signed-in user may delete their own account.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
public sealed record GetAccountDeletionStatusQuery(Guid UserId)
    : IQuery<AccountDeletionStatusResponse>;

/// <summary>
/// Executes the query that tells whether the signed-in user may delete their own account. Every
/// account may, except the initial administrator, so the application always keeps an administrator.
/// </summary>
public sealed class GetAccountDeletionStatusQueryHandler
    : IQueryHandler<GetAccountDeletionStatusQuery, AccountDeletionStatusResponse>
{
    /// <summary>
    /// Handles the request to tell whether the signed-in user may delete their own account.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the deletion status.</returns>
    public Task<AccountDeletionStatusResponse> HandleAsync(
        GetAccountDeletionStatusQuery query,
        CancellationToken ct = default
    )
    {
        return Task.FromResult(
            new AccountDeletionStatusResponse(query.UserId != SeedIds.Users.InitialAdministrator)
        );
    }
}
