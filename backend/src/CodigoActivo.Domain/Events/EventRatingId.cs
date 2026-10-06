using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Identifies an <see cref="EventRating"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct EventRatingId(Guid Value) : IEntityId<EventRatingId>
{
    /// <inheritdoc />
    public static EventRatingId New()
    {
        return new EventRatingId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static EventRatingId From(Guid value)
    {
        return new EventRatingId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
