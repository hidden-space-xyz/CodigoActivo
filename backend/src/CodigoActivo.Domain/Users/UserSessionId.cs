using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Identifies a <see cref="UserSession"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct UserSessionId(Guid Value) : IEntityId<UserSessionId>
{
    /// <inheritdoc />
    public static UserSessionId New()
    {
        return new UserSessionId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static UserSessionId From(Guid value)
    {
        return new UserSessionId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
