using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Terms that participants accept before signing up to the events that link them. Its name is
/// unique across documents.
/// </summary>
public class TermsDocument : IdentifiableEntity, IAggregateRoot
{
    private TermsDocument() { }

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text content.
    /// </summary>
    public string Description { get; private set; } = "{}";

    /// <summary>
    /// Creates a terms document.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="description">Rich-text content.</param>
    /// <param name="id">Stable identifier for seeded documents; a new one otherwise.</param>
    /// <returns>The new terms document.</returns>
    public static TermsDocument Create(string name, string description, Guid? id = null)
    {
        var termsDocument = new TermsDocument { Id = id ?? Guid.NewGuid() };
        termsDocument.Rewrite(name, description);
        return termsDocument;
    }

    /// <summary>
    /// Replaces the name and content of the document.
    /// </summary>
    /// <param name="name">New name; surrounding spaces are removed.</param>
    /// <param name="description">New rich-text content.</param>
    public void Rewrite(string name, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name.Trim();
        Description = description;
    }
}
