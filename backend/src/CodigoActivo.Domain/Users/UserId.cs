using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Identifies a <see cref="User"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct UserId(Guid Value) : IEntityId<UserId>
{
    /// <inheritdoc />
    public static UserId New()
    {
        return new UserId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static UserId From(Guid value)
    {
        return new UserId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
