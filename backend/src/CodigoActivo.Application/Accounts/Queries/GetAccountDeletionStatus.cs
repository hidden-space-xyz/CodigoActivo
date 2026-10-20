using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the request to tell whether the signed-in user may delete their own account.
/// </summary>
public sealed record GetAccountDeletionStatusQuery : IQuery<AccountDeletionStatusResponse>;

/// <summary>
/// Executes the query that tells whether the signed-in user may delete their own account. Every
/// account may, except the initial administrator, so the application always keeps an administrator.
/// </summary>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetAccountDeletionStatusQueryHandler(ICurrentUser currentUser)
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
            new AccountDeletionStatusResponse(
                InitialAdministrator.EnsureMayBeDeleted(currentUser.RequiredId()).IsSuccess
            )
        );
    }
}
