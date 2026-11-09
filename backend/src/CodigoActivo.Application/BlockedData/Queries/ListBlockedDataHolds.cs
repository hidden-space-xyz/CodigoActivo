using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.BlockedData.Contracts;

namespace CodigoActivo.Application.BlockedData.Queries;

/// <summary>
/// Carries the request to list the holds on the blocked data.
/// </summary>
/// <param name="Filters">Paging and sorting.</param>
public sealed record ListBlockedDataHoldsQuery(BlockedDataHoldListQuery Filters)
    : IQuery<PagedResult<BlockedDataHoldResponse>>;

/// <summary>
/// Executes the query that lists the holds on the blocked data, newest first by default, so every
/// administrator can see which claims keep history past its retention and since when, but not
/// whose.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListBlockedDataHoldsQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListBlockedDataHoldsQuery, PagedResult<BlockedDataHoldResponse>>
{
    private static readonly SortMap<BlockedDataHoldRow> Sort = new SortMap<BlockedDataHoldRow>()
        .Add("placedAt", h => h.PlacedAt)
        .Add("reference", h => h.Reference)
        .Default("-placedAt")
        .Tie(h => h.Id);

    /// <summary>
    /// Handles the request to list the holds on the blocked data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a page of holds.</returns>
    public Task<PagedResult<BlockedDataHoldResponse>> HandleAsync(
        ListBlockedDataHoldsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = query.Filters;
        var source = BlockedDataProjections.Holds(
            Sort.Apply(readStore.BlockedDataHolds, filters.Sort),
            readStore.Users
        );
        return executor.ToPagedAsync(source, filters.Page, filters.PageSize, ct);
    }
}
