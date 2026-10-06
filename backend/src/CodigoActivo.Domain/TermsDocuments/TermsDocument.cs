using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Terms that participants accept before signing up to the events that link them. Its name is
/// unique across documents.
/// </summary>
public class TermsDocument : AggregateRoot<TermsDocumentId>
{
    private TermsDocument() { }

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text content.
    /// </summary>
    public RichText Description { get; private set; } = RichText.Empty;

    /// <summary>
    /// Creates a terms document.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="description">Rich-text content.</param>
    /// <param name="id">Stable identifier for seeded documents; a new one otherwise.</param>
    /// <returns>The new terms document.</returns>
    public static TermsDocument Create(
        string name,
        RichText description,
        TermsDocumentId? id = null
    )
    {
        var termsDocument = new TermsDocument { Id = id ?? TermsDocumentId.New() };
        termsDocument.Apply(name, description);
        return termsDocument;
    }

    /// <summary>
    /// Replaces the name and content of the document.
    /// </summary>
    /// <param name="name">New name; surrounding spaces are removed.</param>
    /// <param name="description">New rich-text content.</param>
    public void Rewrite(string name, RichText description)
    {
        var previousDescription = Description;
        Apply(name, description);
        Raise(
            new TermsDocumentRewritten(
                Id,
                RichTextFileReferences.ExtractRemoved(previousDescription, Description)
            )
        );
    }

    /// <summary>
    /// Marks the document as deleted, so the files it references can be released once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new TermsDocumentDeleted(Id, RichTextFileReferences.Extract(Description)));
    }

    private void Apply(string name, RichText description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name.Trim();
        Description = description;
    }
}
