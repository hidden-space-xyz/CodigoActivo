namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// One of the choices of a list column, shown with its own color; part of the inventory category
/// aggregate. Its name is unique within the column, ignoring letter case.
/// </summary>
public class InventoryColumnOption : Entity<InventoryColumnOptionId>
{
    /// <summary>
    /// Longest name of an option.
    /// </summary>
    public const int NameMaxLength = 60;

    private InventoryColumnOption() { }

    internal InventoryColumnOption(
        InventoryColumnId columnId,
        InventoryColumnOptionDefinition definition,
        int position
    )
    {
        Id = definition.Id ?? InventoryColumnOptionId.New();
        ColumnId = columnId;
        Redefine(definition, position);
    }

    /// <summary>
    /// Gets the identifier of the column that lists the option.
    /// </summary>
    public InventoryColumnId ColumnId { get; private set; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display color, written as <c>#RRGGBB</c>.
    /// </summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the place of the option in its column, from 0; a list column sorts by it.
    /// </summary>
    public int Position { get; private set; }

    internal void Redefine(InventoryColumnOptionDefinition definition, int position)
    {
        Name = definition.Name.Trim();
        Color = definition.Color.Trim();
        Position = position;
    }
}
