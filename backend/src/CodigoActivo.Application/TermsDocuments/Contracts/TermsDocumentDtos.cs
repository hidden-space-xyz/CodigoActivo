using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Contains the terms document data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the terms document.</param>
/// <param name="Name">Name, unique among terms documents.</param>
/// <param name="Description">Content of the current version as rich text JSON.</param>
/// <param name="RequiresSignature">Whether accepting the document asks for a handwritten signature.</param>
/// <param name="Kind">Whether the document holds conditions or a consent.</param>
/// <param name="Version">Number of the current version; it grows with every change.</param>
public record TermsDocumentResponse(
    Guid Id,
    string Name,
    string Description,
    bool RequiresSignature,
    TermsDocumentKind Kind,
    int Version
)
{
    /// <summary>
    /// Initializes an empty terms document response for serialization.
    /// </summary>
    public TermsDocumentResponse()
        : this(Guid.Empty, string.Empty, string.Empty, false, TermsDocumentKind.Conditions, 0) { }
}
