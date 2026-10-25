using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Identifies a <see cref="BlockedDataConsultation"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct BlockedDataConsultationId(Guid Value)
    : IEntityId<BlockedDataConsultationId>
{
    /// <inheritdoc />
    public static BlockedDataConsultationId New()
    {
        return new BlockedDataConsultationId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static BlockedDataConsultationId From(Guid value)
    {
        return new BlockedDataConsultationId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
