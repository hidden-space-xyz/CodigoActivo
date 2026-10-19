using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Contains the terms document data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
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
