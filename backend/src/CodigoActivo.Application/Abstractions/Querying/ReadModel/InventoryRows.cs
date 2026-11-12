namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored inventory category as the queries read it.
/// </summary>
public sealed class InventoryCategoryRow
{
    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the columns.
    /// </summary>
    public ICollection<InventoryColumnRow> Columns { get; init; } = [];

    /// <summary>
    /// Gets the entries.
    /// </summary>
    public ICollection<InventoryEntryRow> Entries { get; init; } = [];
}

/// <summary>
/// Stored column of an inventory category as the queries read it.
/// </summary>
public sealed class InventoryColumnRow
{
    /// <summary>
    /// Gets the identifier of the column.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    public Guid CategoryId { get; init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the kind of value the column holds.
    /// </summary>
    public InventoryColumnType Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether every entry has a value in the column.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the place of the column in its category, from 0.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Gets the options of a list column.
    /// </summary>
    public ICollection<InventoryColumnOptionRow> Options { get; init; } = [];
}

/// <summary>
/// Stored option of a list column as the queries read it.
/// </summary>
public sealed class InventoryColumnOptionRow
{
    /// <summary>
    /// Gets the identifier of the option.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the identifier of the column.
    /// </summary>
    public Guid ColumnId { get; init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display color, written as <c>#RRGGBB</c>.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// Gets the place of the option in its column, from 0.
    /// </summary>
    public int Position { get; init; }
}

/// <summary>
/// Stored entry of an inventory category as the queries read it.
/// </summary>
public sealed class InventoryEntryRow
{
    /// <summary>
    /// Gets the identifier of the entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    public Guid CategoryId { get; init; }

    /// <summary>
    /// Gets the value of the quantity column, if any.
    /// </summary>
    public int? Quantity { get; init; }

    /// <summary>
    /// Gets the JSON object of the other values, keyed by column identifier.
    /// </summary>
    public string Values { get; init; } = "{}";
}
