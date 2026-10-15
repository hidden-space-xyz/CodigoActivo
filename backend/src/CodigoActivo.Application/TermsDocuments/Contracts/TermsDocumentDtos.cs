namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Contains the terms document data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="RequiresSignature">Whether accepting the document asks for a handwritten signature.</param>
public record TermsDocumentResponse(
    Guid Id,
    string Name,
    string Description,
    bool RequiresSignature
)
{
    /// <summary>
    /// Initializes an empty terms document response for serialization.
    /// </summary>
    public TermsDocumentResponse()
        : this(Guid.Empty, string.Empty, string.Empty, false) { }
}
