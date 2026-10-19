using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// A version of a terms document that a later rewrite replaced: its name, content, whether
/// accepting it asked for a signature and its kind, as they stood then. It is kept so the text a
/// decision was taken on can always be shown, whatever the document says now.
/// </summary>
public class TermsDocumentVersion
{
    private TermsDocumentVersion() { }

    internal TermsDocumentVersion(TermsDocument document)
    {
        TermsDocumentId = document.Id;
        Number = document.Version;
        Name = document.Name;
        Description = document.Description;
        RequiresSignature = document.RequiresSignature;
        Kind = document.Kind;
    }

    /// <summary>
    /// Gets the identifier of the terms document.
    /// </summary>
    public TermsDocumentId TermsDocumentId { get; private set; }

    /// <summary>
    /// Gets the number of the version, from <see cref="TermsDocument.FirstVersion"/> up.
    /// </summary>
    public int Number { get; private set; }

    /// <summary>
    /// Gets the name the document had in this version.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text content of this version.
    /// </summary>
    public RichText Description { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets a value indicating whether accepting this version asked for a handwritten signature.
    /// </summary>
    public bool RequiresSignature { get; private set; }

    /// <summary>
    /// Gets whether this version held conditions or a consent.
    /// </summary>
    public TermsDocumentKind Kind { get; private set; }
}
