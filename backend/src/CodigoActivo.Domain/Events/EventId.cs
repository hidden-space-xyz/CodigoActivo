using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Identifies an <see cref="Event"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct EventId(Guid Value) : IEntityId<EventId>
{
    /// <inheritdoc />
    public static EventId New()
    {
        return new EventId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static EventId From(Guid value)
    {
        return new EventId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
