using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.UnitTests.Application.Inventory;

internal static class InventoryRowData
{
    public static InventoryCategoryRow CategoryRow(
        string name,
        IEnumerable<InventoryColumnRow> columns,
        params InventoryEntryRow[] entries
    )
    {
        var id = Guid.NewGuid();
        return new InventoryCategoryRow
        {
            Id = id,
            Name = name,
            Columns = [.. columns],
            Entries = [.. entries.Select(entry => WithCategory(entry, id))],
        };
    }

    public static InventoryColumnRow ColumnRow(
        string name,
        InventoryColumnType type,
        int position,
        params InventoryColumnOptionRow[] options
    )
    {
        return new InventoryColumnRow
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Position = position,
            Options = options,
        };
    }

    public static InventoryColumnOptionRow OptionRow(string name, int position)
    {
        return new InventoryColumnOptionRow
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = "#112233",
            Position = position,
        };
    }

    public static InventoryEntryRow EntryRow(int? quantity = null, string values = "{}")
    {
        return new InventoryEntryRow
        {
            Id = Guid.NewGuid(),
            Quantity = quantity,
            Values = values,
        };
    }

    private static InventoryEntryRow WithCategory(InventoryEntryRow entry, Guid categoryId)
    {
        return new InventoryEntryRow
        {
            Id = entry.Id,
            CategoryId = categoryId,
            Quantity = entry.Quantity,
            Values = entry.Values,
        };
    }
}
