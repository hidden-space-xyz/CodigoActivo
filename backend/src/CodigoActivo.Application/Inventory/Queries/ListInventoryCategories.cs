using CodigoActivo.Application.Inventory.Contracts;

namespace CodigoActivo.Application.Inventory.Queries;

/// <summary>
/// Carries the criteria used to list inventory categories.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListInventoryCategoriesQuery(InventoryCategoryListQuery Filters)
    : IQuery<PagedResult<InventoryCategorySummaryResponse>>;

/// <summary>
/// Executes the query to list inventory categories with their number of entries and total.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListInventoryCategoriesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListInventoryCategoriesQuery, PagedResult<InventoryCategorySummaryResponse>>
{
    private static readonly SortMap<InventoryCategorySummaryResponse> Sort =
        new SortMap<InventoryCategorySummaryResponse>()
            .Add("name", c => c.Name)
            .Add("entryCount", c => c.EntryCount)
            .Add("total", c => c.Total)
            .Default("name")
            .Tie(c => c.Id);

    /// <summary>
    /// Handles the request to list inventory categories.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the requested page of categories, by name unless the sort
    /// names <c>entryCount</c> or <c>total</c>.
    /// </returns>
    public Task<PagedResult<InventoryCategorySummaryResponse>> HandleAsync(
        ListInventoryCategoriesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = query.Filters;
        var source = readStore.InventoryCategories.Select(InventoryProjections.Summary);
        source = source.WhereContains(c => c.Name, filters.Name);
        source = Sort.Apply(source, filters.Sort);
        return executor.ToPagedAsync(source, filters.Page, filters.PageSize, ct);
    }
}
