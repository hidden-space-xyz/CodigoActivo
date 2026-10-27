using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Terms that participants accept or decline when signing up to the events that link them: the
/// conditions of taking part or a consent, as its <see cref="Kind"/> tells. Its name is unique
/// across documents. Every rewrite that changes it starts a new <see cref="Version"/> and keeps the
/// one it replaces in <see cref="PreviousVersions"/>, so each decision can name the text it was
/// taken on.
/// </summary>
public class TermsDocument : AggregateRoot<TermsDocumentId>
{
    /// <summary>
    /// Number of the version a document starts with.
    /// </summary>
    public const int FirstVersion = 1;

    private readonly List<TermsDocumentVersion> previousVersions = [];

    private TermsDocument() { }

    /// <summary>
    /// Gets the number of the current version.
    /// </summary>
    public int Version { get; private set; } = FirstVersion;

    /// <summary>
    /// Gets the versions later rewrites replaced.
    /// </summary>
    public IReadOnlyCollection<TermsDocumentVersion> PreviousVersions => previousVersions;

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text content.
    /// </summary>
    public RichText Description { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets a value indicating whether accepting the document also asks for a handwritten
    /// signature.
    /// </summary>
    public bool RequiresSignature { get; private set; }

    /// <summary>
    /// Gets whether the document holds conditions or a consent.
    /// </summary>
    public TermsDocumentKind Kind { get; private set; } = TermsDocumentKind.Conditions;

    /// <summary>
    /// Creates a terms document.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="description">Rich-text content.</param>
    /// <param name="requiresSignature">Whether accepting it asks for a handwritten signature.</param>
    /// <param name="kind">Whether it holds conditions or a consent.</param>
    /// <param name="id">Stable identifier for seeded documents; a new one otherwise.</param>
    /// <returns>The new terms document.</returns>
    public static TermsDocument Create(
        string name,
        RichText description,
        bool requiresSignature,
        TermsDocumentKind kind,
        TermsDocumentId? id = null
    )
    {
        var termsDocument = new TermsDocument { Id = id ?? TermsDocumentId.New() };
        termsDocument.Apply(name, description, requiresSignature, kind);
        return termsDocument;
    }

    /// <summary>
    /// Replaces the name and content of the document, whether accepting it asks for a signature and
    /// its kind. When anything changes, the current version is kept in
    /// <see cref="PreviousVersions"/> and the new one gets the next number, so decisions already
    /// taken keep naming the text they were taken on; nobody has to decide again. Every rewrite
    /// raises <see cref="TermsDocumentRewritten"/> with the images the content no longer references.
    /// </summary>
    /// <param name="name">New name; surrounding spaces are removed.</param>
    /// <param name="description">New rich-text content.</param>
    /// <param name="requiresSignature">Whether accepting it asks for a handwritten signature.</param>
    /// <param name="kind">Whether it holds conditions or a consent.</param>
    public void Rewrite(
        string name,
        RichText description,
        bool requiresSignature,
        TermsDocumentKind kind
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        var previousDescription = Description;
        if (
            Name != name.Trim()
            || !Description.HasSameContentAs(description)
            || RequiresSignature != requiresSignature
            || Kind != kind
        )
        {
            previousVersions.Add(new TermsDocumentVersion(this));
            Version++;
        }

        Apply(name, description, requiresSignature, kind);
        Raise(
            new TermsDocumentRewritten(
                Id,
                RichTextFileReferences.ExtractRemoved(previousDescription, Description)
            )
        );
    }

    /// <summary>
    /// Raises <see cref="TermsDocumentDeleted"/> before the document is removed, so the files it
    /// references are released once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new TermsDocumentDeleted(Id, RichTextFileReferences.Extract(Description)));
    }

    private void Apply(
        string name,
        RichText description,
        bool requiresSignature,
        TermsDocumentKind kind
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name.Trim();
        Description = description;
        RequiresSignature = requiresSignature;
        Kind = kind;
    }
}
