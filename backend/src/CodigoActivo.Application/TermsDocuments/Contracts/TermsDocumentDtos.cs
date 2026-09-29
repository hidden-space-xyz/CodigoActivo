using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Contains the terms document data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public record TermsDocumentResponse(Guid Id, string Name, string Description)
{
    /// <summary>
    /// Initializes an empty terms document response for serialization.
    /// </summary>
    public TermsDocumentResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the client-supplied data used to create a terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
public record CreateTermsDocumentRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [JsonString] [NoRichTextImages] [MaxLength(262144)] string Description
);

/// <summary>
/// Contains the client-supplied data used to update the terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
public record UpdateTermsDocumentRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [JsonString] [NoRichTextImages] [MaxLength(262144)] string Description
);
