using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// What a column of an inventory category should become.
/// </summary>
/// <param name="Id">Identifier of an existing column of the category; <see langword="null"/> adds one.</param>
/// <param name="Name">
/// Name of up to <see cref="InventoryColumn.NameMaxLength"/> characters, unique within the category.
/// </param>
/// <param name="Type">Kind of value the column holds.</param>
/// <param name="Required">Whether every entry must have a value in it.</param>
/// <param name="Options">
/// Options of a list column in order, up to <see cref="InventoryColumn.MaxOptions"/>; empty for any
/// other type.
/// </param>
public sealed record InventoryColumnDraft(
    InventoryColumnId? Id,
    [property: Required, MaxLength(InventoryColumn.NameMaxLength)] string Name,
    InventoryColumnType Type,
    bool Required,
    [property: Required, MaxLength(InventoryColumn.MaxOptions)]
        IReadOnlyList<InventoryColumnOptionDraft> Options
)
{
    /// <summary>
    /// Gets the definition the column is created or changed with.
    /// </summary>
    /// <returns>The definition.</returns>
    public InventoryColumnDefinition ToDefinition()
    {
        return new InventoryColumnDefinition(
            Id,
            Name,
            Type,
            Required,
            [.. Options.Select(option => option.ToDefinition())]
        );
    }
}

/// <summary>
/// What an option of a list column should become.
/// </summary>
/// <param name="Id">Identifier of an existing option of the column; <see langword="null"/> adds one.</param>
/// <param name="Name">
/// Name of up to <see cref="InventoryColumnOption.NameMaxLength"/> characters, unique within the
/// column.
/// </param>
/// <param name="Color">Display color as <c>#RRGGBB</c>.</param>
public sealed record InventoryColumnOptionDraft(
    InventoryColumnOptionId? Id,
    [property: Required, MaxLength(InventoryColumnOption.NameMaxLength)] string Name,
    [property: Required, MaxLength(9), RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
)
{
    /// <summary>
    /// Gets the definition the option is created or changed with.
    /// </summary>
    /// <returns>The definition.</returns>
    public InventoryColumnOptionDefinition ToDefinition()
    {
        return new InventoryColumnOptionDefinition(Id, Name, Color);
    }
}
