using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Partners;

/// <summary>
/// Identifies a <see cref="Partner"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct PartnerId(Guid Value) : IEntityId<PartnerId>
{
    /// <inheritdoc />
    public static PartnerId New()
    {
        return new PartnerId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static PartnerId From(Guid value)
    {
        return new PartnerId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
