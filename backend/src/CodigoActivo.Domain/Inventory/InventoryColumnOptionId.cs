namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Identifies an <see cref="InventoryColumnOption"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct InventoryColumnOptionId(Guid Value)
    : IEntityId<InventoryColumnOptionId>
{
    /// <inheritdoc />
    public static InventoryColumnOptionId New()
    {
        return new InventoryColumnOptionId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static InventoryColumnOptionId From(Guid value)
    {
        return new InventoryColumnOptionId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
