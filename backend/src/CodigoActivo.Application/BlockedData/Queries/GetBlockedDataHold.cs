using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Queries;

/// <summary>
/// Carries the request for one hold on the blocked data.
/// </summary>
/// <param name="HoldId">Identifier of the hold.</param>
public sealed record GetBlockedDataHoldQuery(BlockedDataHoldId HoldId)
    : IQuery<Result<BlockedDataHoldResponse>>;

/// <summary>
/// Executes the query for one hold on the blocked data, without the accounts it keeps.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetBlockedDataHoldQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetBlockedDataHoldQuery, Result<BlockedDataHoldResponse>>
{
    /// <summary>
    /// Handles the request for one hold on the blocked data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the hold, or the error.</returns>
    public async Task<Result<BlockedDataHoldResponse>> HandleAsync(
        GetBlockedDataHoldQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = query.HoldId.Value;
        var hold = await executor.FirstOrDefaultAsync(
            BlockedDataProjections.Holds(
                readStore.BlockedDataHolds.Where(h => h.Id == id),
                readStore.Users
            ),
            ct
        );
        return hold is null ? Error.NotFound(DomainErrorCode.BlockedDataHoldNotFound) : hold;
    }
}
