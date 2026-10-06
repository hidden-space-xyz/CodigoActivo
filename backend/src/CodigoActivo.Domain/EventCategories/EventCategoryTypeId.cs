using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.EventCategories;

/// <summary>
/// Identifies an <see cref="EventCategoryType"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct EventCategoryTypeId(Guid Value) : IEntityId<EventCategoryTypeId>
{
    /// <inheritdoc />
    public static EventCategoryTypeId New()
    {
        return new EventCategoryTypeId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static EventCategoryTypeId From(Guid value)
    {
        return new EventCategoryTypeId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
