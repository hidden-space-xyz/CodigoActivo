using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Security;

namespace CodigoActivo.Application.BlockedData.Queries;

/// <summary>
/// Carries the request for whether the signed-in administrator may consult the blocked data.
/// </summary>
public sealed record GetBlockedDataAccessQuery : IQuery<BlockedDataAccessResponse>;

/// <summary>
/// Executes the query that tells the signed-in administrator whether they are the account
/// designated for data protection, with the kinds of data an extract can be narrowed to.
/// </summary>
/// <param name="readStore">Read side of the accounts.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Signed-in user.</param>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="history">Reader of the data history.</param>
public sealed class GetBlockedDataAccessQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser,
    PrivacyOfficer officer,
    IDataHistoryReader history
) : IQueryHandler<GetBlockedDataAccessQuery, BlockedDataAccessResponse>
{
    private static readonly Guid ActiveStatusId = CatalogIds.UserStatuses.IdOf(
        Domain.Users.UserStatus.Active
    );

    /// <summary>
    /// Handles the request for whether the signed-in administrator may consult the blocked data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result tells what the account may do.</returns>
    public async Task<BlockedDataAccessResponse> HandleAsync(
        GetBlockedDataAccessQuery query,
        CancellationToken ct = default
    )
    {
        var userId = currentUser.RequiredId().Value;
        var account = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.IsAdmin,
                    Active = u.UserStatusTypeId == ActiveStatusId,
                    u.Email,
                }),
            ct
        );
        return new BlockedDataAccessResponse(
            account is not null
                && officer.Designates(account.IsAdmin, account.Active, account.Email),
            history.Entities
        );
    }
}
