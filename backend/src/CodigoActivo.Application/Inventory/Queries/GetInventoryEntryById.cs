using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Queries;

/// <summary>
/// Carries the criteria used to retrieve an entry of an inventory category.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
/// <param name="EntryId">Identifier of the entry.</param>
public sealed record GetInventoryEntryByIdQuery(
    InventoryCategoryId CategoryId,
    InventoryEntryId EntryId
) : IQuery<Result<InventoryEntryResponse>>;

/// <summary>
/// Executes the query to retrieve an entry of an inventory category.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetInventoryEntryByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetInventoryEntryByIdQuery, Result<InventoryEntryResponse>>
{
    /// <summary>
    /// Handles the request to retrieve an inventory entry.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the entry;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>; or
    /// <see cref="DomainErrorCode.InventoryEntryNotFound"/>.
    /// </returns>
    public async Task<Result<InventoryEntryResponse>> HandleAsync(
        GetInventoryEntryByIdQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var categoryId = query.CategoryId.Value;
        var entry = await executor.FirstOrDefaultAsync(
            readStore.InventoryEntries.Where(entry =>
                entry.CategoryId == categoryId && entry.Id == query.EntryId.Value
            ),
            ct
        );
        if (entry is not null)
        {
            return InventoryProjections.Entry(entry);
        }

        return await executor.AnyAsync(
            readStore.InventoryCategories.Where(category => category.Id == categoryId),
            ct
        )
            ? Error.NotFound(DomainErrorCode.InventoryEntryNotFound)
            : Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
    }
}
