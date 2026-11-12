using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Queries;

/// <summary>
/// Carries the criteria used to list the entries of an inventory category.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
public sealed record ListInventoryEntriesQuery(InventoryCategoryId CategoryId)
    : IQuery<Result<IReadOnlyList<InventoryEntryResponse>>>;

/// <summary>
/// Executes the query to list every entry of an inventory category, which holds at most
/// <see cref="InventoryCategory.MaxEntries"/>; clients filter and sort them.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListInventoryEntriesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListInventoryEntriesQuery, Result<IReadOnlyList<InventoryEntryResponse>>>
{
    /// <summary>
    /// Handles the request to list the entries of an inventory category.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the entries in a stable order, or
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>.
    /// </returns>
    public async Task<Result<IReadOnlyList<InventoryEntryResponse>>> HandleAsync(
        ListInventoryEntriesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var categoryId = query.CategoryId.Value;
        var entries = await executor.ToListAsync(
            readStore
                .InventoryEntries.Where(entry => entry.CategoryId == categoryId)
                .OrderBy(entry => entry.Id),
            ct
        );
        if (
            entries.Count == 0
            && !await executor.AnyAsync(
                readStore.InventoryCategories.Where(category => category.Id == categoryId),
                ct
            )
        )
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        return Result.Success<IReadOnlyList<InventoryEntryResponse>>([
            .. entries.Select(InventoryProjections.Entry),
        ]);
    }
}
