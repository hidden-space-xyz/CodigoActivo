namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// One row of an inventory category: a single thing, or a group of things counted by the
/// quantity column; part of the inventory category aggregate.
/// </summary>
public class InventoryEntry : Entity<InventoryEntryId>
{
    /// <summary>
    /// Largest quantity an entry counts.
    /// </summary>
    public const int MaxQuantity = 1_000_000;

    private InventoryEntry() { }

    internal InventoryEntry(InventoryCategoryId categoryId, int? quantity, InventoryValues values)
    {
        CategoryId = categoryId;
        Quantity = quantity;
        Values = values;
    }

    /// <summary>
    /// Gets the identifier of the category the entry belongs to.
    /// </summary>
    public InventoryCategoryId CategoryId { get; private set; }

    /// <summary>
    /// Gets the value of the quantity column, or <see langword="null"/> when it has none or the
    /// category has no such column.
    /// </summary>
    public int? Quantity { get; private set; }

    /// <summary>
    /// Gets the values of every other column.
    /// </summary>
    public InventoryValues Values { get; private set; } = InventoryValues.Empty;

    internal bool HasValueIn(InventoryColumn column)
    {
        return column.Type is InventoryColumnType.Quantity
            ? Quantity is not null
            : Values.Of(column.Id) is not null;
    }

    internal bool Chose(InventoryColumn column, InventoryColumnOptionId option)
    {
        return Values.Of(column.Id) == option.Value.ToString();
    }

    internal void Replace(int? quantity, InventoryValues values)
    {
        Quantity = quantity;
        Values = values;
    }

    internal void Clear(InventoryColumn column)
    {
        if (column.Type is InventoryColumnType.Quantity)
        {
            Quantity = null;
        }
        else
        {
            Values = Values.Without(column.Id);
        }
    }
}
