using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Identifies a <see cref="Resource"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct ResourceId(Guid Value) : IEntityId<ResourceId>
{
    /// <inheritdoc />
    public static ResourceId New()
    {
        return new ResourceId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static ResourceId From(Guid value)
    {
        return new ResourceId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
