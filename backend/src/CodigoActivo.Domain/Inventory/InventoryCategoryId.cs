namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Identifies an <see cref="InventoryCategory"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct InventoryCategoryId(Guid Value) : IEntityId<InventoryCategoryId>
{
    /// <inheritdoc />
    public static InventoryCategoryId New()
    {
        return new InventoryCategoryId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static InventoryCategoryId From(Guid value)
    {
        return new InventoryCategoryId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
