using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Identifies an <see cref="Activity"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct ActivityId(Guid Value) : IEntityId<ActivityId>
{
    /// <inheritdoc />
    public static ActivityId New()
    {
        return new ActivityId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static ActivityId From(Guid value)
    {
        return new ActivityId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
