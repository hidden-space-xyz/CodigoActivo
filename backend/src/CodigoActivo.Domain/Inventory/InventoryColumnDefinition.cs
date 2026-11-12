namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// What a column of an inventory category should become when the category is created or
/// redefined.
/// </summary>
/// <param name="Id">Identifier of an existing column of the category; <see langword="null"/> adds one.</param>
/// <param name="Name">Name; surrounding spaces are removed.</param>
/// <param name="Type">Kind of value the column holds.</param>
/// <param name="Required">Whether every entry must have a value in it.</param>
/// <param name="Options">Choices of a list column, in order; empty for any other type.</param>
public sealed record InventoryColumnDefinition(
    InventoryColumnId? Id,
    string Name,
    InventoryColumnType Type,
    bool Required,
    IReadOnlyList<InventoryColumnOptionDefinition> Options
);

/// <summary>
/// What a choice of a list column should become.
/// </summary>
/// <param name="Id">Identifier of an existing option of the column; <see langword="null"/> adds one.</param>
/// <param name="Name">Name; surrounding spaces are removed.</param>
/// <param name="Color">Display color, written as <c>#RRGGBB</c>.</param>
public sealed record InventoryColumnOptionDefinition(
    InventoryColumnOptionId? Id,
    string Name,
    string Color
);
