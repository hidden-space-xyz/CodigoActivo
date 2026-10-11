using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Identifies the second-factor challenge an account has open, which the challenge cookie names.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct LoginChallengeId(Guid Value) : IEntityId<LoginChallengeId>
{
    /// <inheritdoc />
    public static LoginChallengeId New()
    {
        return new LoginChallengeId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static LoginChallengeId From(Guid value)
    {
        return new LoginChallengeId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
