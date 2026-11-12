namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Identifies an <see cref="InventoryEntry"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct InventoryEntryId(Guid Value) : IEntityId<InventoryEntryId>
{
    /// <inheritdoc />
    public static InventoryEntryId New()
    {
        return new InventoryEntryId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static InventoryEntryId From(Guid value)
    {
        return new InventoryEntryId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
