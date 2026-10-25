using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.BlockedData.Queries;

/// <summary>
/// Carries the request to list the consultations of the blocked data.
/// </summary>
/// <param name="Filters">Paging and sorting.</param>
public sealed record ListBlockedDataConsultationsQuery(BlockedDataConsultationListQuery Filters)
    : IQuery<PagedResult<BlockedDataConsultationResponse>>;

/// <summary>
/// Executes the query that lists the consultations of the blocked data, newest first by default,
/// so every administrator can see who consulted it, when and for which request, but none of the
/// data.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListBlockedDataConsultationsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListBlockedDataConsultationsQuery, PagedResult<BlockedDataConsultationResponse>>
{
    private static readonly SortMap<BlockedDataConsultationRow> Sort =
        new SortMap<BlockedDataConsultationRow>()
            .Add("openedAt", c => c.OpenedAt)
            .Add("reference", c => c.Reference)
            .Default("-openedAt")
            .Tie(c => c.Id);

    /// <summary>
    /// Handles the request to list the consultations of the blocked data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a page of consultations.</returns>
    public Task<PagedResult<BlockedDataConsultationResponse>> HandleAsync(
        ListBlockedDataConsultationsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = query.Filters;
        var source = BlockedDataProjections.Consultations(
            Sort.Apply(readStore.BlockedDataConsultations, filters.Sort),
            readStore.Users
        );
        return executor.ToPagedAsync(source, filters.Page, filters.PageSize, ct);
    }
}
