using System.Linq.Expressions;
using System.Text.Json;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Inventory.Contracts;

namespace CodigoActivo.Application.Inventory;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class InventoryProjections
{
    /// <summary>
    /// Projects a category with its number of entries and its total.
    /// </summary>
    public static readonly Expression<
        Func<InventoryCategoryRow, InventoryCategorySummaryResponse>
    > Summary = WithTotal(
        (category, total) =>
            new InventoryCategorySummaryResponse
            {
                Id = category.Id,
                Name = category.Name,
                EntryCount = category.Entries.Count,
                Total = total,
            }
    );

    /// <summary>
    /// Projects a category with its columns and their options, in order.
    /// </summary>
    public static readonly Expression<
        Func<InventoryCategoryRow, InventoryCategoryResponse>
    > Category = WithTotal(
        (category, total) =>
            new InventoryCategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Columns = category
                    .Columns.OrderBy(column => column.Position)
                    .Select(column => new InventoryColumnResponse
                    {
                        Id = column.Id,
                        Name = column.Name,
                        Type = column.Type,
                        Required = column.Required,
                        Options = column
                            .Options.OrderBy(option => option.Position)
                            .Select(option => new InventoryColumnOptionResponse
                            {
                                Id = option.Id,
                                Name = option.Name,
                                Color = option.Color,
                            })
                            .ToList(),
                    })
                    .ToList(),
                EntryCount = category.Entries.Count,
                Total = total,
            }
    );

    /// <summary>
    /// Builds the response of an entry from its row, reading the stored values.
    /// </summary>
    /// <param name="entry">Row of the entry.</param>
    /// <returns>The entry.</returns>
    public static InventoryEntryResponse Entry(InventoryEntryRow entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new InventoryEntryResponse(
            entry.Id,
            entry.Quantity,
            JsonSerializer.Deserialize<Dictionary<Guid, string>>(entry.Values) ?? []
        );
    }

    private static Expression<Func<InventoryCategoryRow, TResponse>> WithTotal<TResponse>(
        Expression<Func<InventoryCategoryRow, int, TResponse>> projection
    )
    {
        Expression<Func<InventoryCategoryRow, int>> total = category =>
            category.Columns.Any(column => column.Type == InventoryColumnType.Quantity)
                ? category.Entries.Sum(entry => entry.Quantity ?? 0)
                : category.Entries.Count;
        var row = Expression.Parameter(typeof(InventoryCategoryRow), "category");
        return Expression.Lambda<Func<InventoryCategoryRow, TResponse>>(
            Expression.Invoke(projection, row, Expression.Invoke(total, row)),
            row
        );
    }
}
