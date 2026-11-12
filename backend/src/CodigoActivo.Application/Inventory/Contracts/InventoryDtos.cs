using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Inventory.Contracts;

/// <summary>
/// Contains an inventory category as the list of categories shows it.
/// </summary>
/// <param name="Id">Identifier of the category.</param>
/// <param name="Name">Name, unique among categories.</param>
/// <param name="EntryCount">Number of entries.</param>
/// <param name="Total">
/// Sum of the quantity column, or the number of entries when the category has no such column.
/// </param>
public record InventoryCategorySummaryResponse(Guid Id, string Name, int EntryCount, int Total)
{
    /// <summary>
    /// Initializes an empty inventory category summary for serialization.
    /// </summary>
    public InventoryCategorySummaryResponse()
        : this(Guid.Empty, string.Empty, 0, 0) { }
}

/// <summary>
/// Contains an inventory category with the columns its entries fill.
/// </summary>
/// <param name="Id">Identifier of the category.</param>
/// <param name="Name">Name, unique among categories.</param>
/// <param name="Columns">Columns in order.</param>
/// <param name="EntryCount">Number of entries.</param>
/// <param name="Total">
/// Sum of the quantity column, or the number of entries when the category has no such column.
/// </param>
public record InventoryCategoryResponse(
    Guid Id,
    string Name,
    IReadOnlyList<InventoryColumnResponse> Columns,
    int EntryCount,
    int Total
)
{
    /// <summary>
    /// Initializes an empty inventory category response for serialization.
    /// </summary>
    public InventoryCategoryResponse()
        : this(Guid.Empty, string.Empty, [], 0, 0) { }
}

/// <summary>
/// Contains a column of an inventory category.
/// </summary>
/// <param name="Id">Identifier of the column.</param>
/// <param name="Name">Name, unique within the category.</param>
/// <param name="Type">Kind of value the column holds.</param>
/// <param name="Required">Whether every entry has a value in it.</param>
/// <param name="Options">Options of a list column in order; empty for any other type.</param>
public record InventoryColumnResponse(
    Guid Id,
    string Name,
    InventoryColumnType Type,
    bool Required,
    IReadOnlyList<InventoryColumnOptionResponse> Options
)
{
    /// <summary>
    /// Initializes an empty inventory column response for serialization.
    /// </summary>
    public InventoryColumnResponse()
        : this(Guid.Empty, string.Empty, InventoryColumnType.Text, false, []) { }
}

/// <summary>
/// Contains an option of a list column.
/// </summary>
/// <param name="Id">Identifier of the option.</param>
/// <param name="Name">Name, unique within the column.</param>
/// <param name="Color">Display color as <c>#RRGGBB</c>.</param>
public record InventoryColumnOptionResponse(Guid Id, string Name, string Color)
{
    /// <summary>
    /// Initializes an empty inventory column option response for serialization.
    /// </summary>
    public InventoryColumnOptionResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains an entry of an inventory category.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="Quantity">Value of the quantity column, if any.</param>
/// <param name="Values">
/// Value of each other column that has one, keyed by column identifier: text as written, a number
/// with a dot, a day as <c>YYYY-MM-DD</c>, <c>true</c> or <c>false</c>, or the identifier of the
/// chosen option.
/// </param>
public record InventoryEntryResponse(
    Guid Id,
    int? Quantity,
    IReadOnlyDictionary<Guid, string> Values
)
{
    /// <summary>
    /// Initializes an empty inventory entry response for serialization.
    /// </summary>
    public InventoryEntryResponse()
        : this(Guid.Empty, null, new Dictionary<Guid, string>()) { }
}
