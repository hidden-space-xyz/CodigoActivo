using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to tell whether the signed-in user may delete their own account.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="IsAdmin">Whether the signed-in user is an administrator.</param>
public sealed record GetAccountDeletionStatusQuery(Guid UserId, bool IsAdmin)
    : IQuery<AccountDeletionStatusResponse>;

/// <summary>
/// Executes the query that tells whether the signed-in user may delete their own account. Every
/// account may, except the last administrator, so the application always keeps one.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetAccountDeletionStatusQueryHandler(
    IUserRepository users,
    IQueryExecutor executor
) : IQueryHandler<GetAccountDeletionStatusQuery, AccountDeletionStatusResponse>
{
    /// <summary>
    /// Handles the request to tell whether the signed-in user may delete their own account.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the deletion status.</returns>
    public async Task<AccountDeletionStatusResponse> HandleAsync(
        GetAccountDeletionStatusQuery query,
        CancellationToken ct = default
    )
    {
        if (!query.IsAdmin)
        {
            return new AccountDeletionStatusResponse(true);
        }

        var anotherAdminExists = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.IsAdmin && u.Id != query.UserId).Select(_ => true),
            ct
        );
        return new AccountDeletionStatusResponse(anotherAdminExists);
    }
}
