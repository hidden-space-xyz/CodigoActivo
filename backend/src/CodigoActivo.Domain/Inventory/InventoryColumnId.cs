namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Identifies an <see cref="InventoryColumn"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct InventoryColumnId(Guid Value) : IEntityId<InventoryColumnId>
{
    /// <inheritdoc />
    public static InventoryColumnId New()
    {
        return new InventoryColumnId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static InventoryColumnId From(Guid value)
    {
        return new InventoryColumnId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
