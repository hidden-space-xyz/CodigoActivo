using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// Identifies a <see cref="LegalPolicy"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct LegalPolicyId(Guid Value) : IEntityId<LegalPolicyId>
{
    /// <inheritdoc />
    public static LegalPolicyId New()
    {
        return new LegalPolicyId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static LegalPolicyId From(Guid value)
    {
        return new LegalPolicyId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
