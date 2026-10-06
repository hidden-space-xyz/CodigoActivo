using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Identifies a <see cref="TermsDocument"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct TermsDocumentId(Guid Value) : IEntityId<TermsDocumentId>
{
    /// <inheritdoc />
    public static TermsDocumentId New()
    {
        return new TermsDocumentId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static TermsDocumentId From(Guid value)
    {
        return new TermsDocumentId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
