namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Identifies a <see cref="BlockedDataHold"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct BlockedDataHoldId(Guid Value) : IEntityId<BlockedDataHoldId>
{
    /// <inheritdoc />
    public static BlockedDataHoldId New()
    {
        return new BlockedDataHoldId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static BlockedDataHoldId From(Guid value)
    {
        return new BlockedDataHoldId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
