using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Mapping;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Domain.TermsDocuments;
using TermsDocumentKind = CodigoActivo.Application.Abstractions.Querying.ReadModel.TermsDocumentKind;

namespace CodigoActivo.API.TermsDocuments.Contracts;

/// <summary>
/// Contains the client-supplied data used to create a terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
/// <param name="RequiresSignature">Whether accepting the document asks for a handwritten signature.</param>
/// <param name="Kind">Whether the document holds conditions or a consent.</param>
public record CreateTermsDocumentRequest(
    [Required] [MaxLength(120)] string Name,
    [Required] [MaxLength(262144)] string Description,
    [EnumDataType(typeof(TermsDocumentKind))] TermsDocumentKind Kind,
    bool RequiresSignature = false
)
{
    /// <summary>
    /// Builds the command that creates the terms document.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateTermsDocumentCommand ToCommand()
    {
        return new CreateTermsDocumentCommand(
            Name,
            Description,
            RequiresSignature,
            Kind.ToDomain()
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to update the terms document.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
/// <param name="RequiresSignature">Whether accepting the document asks for a handwritten signature.</param>
/// <param name="Kind">Whether the document holds conditions or a consent.</param>
public record UpdateTermsDocumentRequest(
    [Required] [MaxLength(120)] string Name,
    [Required] [MaxLength(262144)] string Description,
    [EnumDataType(typeof(TermsDocumentKind))] TermsDocumentKind Kind,
    bool RequiresSignature = false
)
{
    /// <summary>
    /// Builds the command that updates the terms document.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <returns>The command.</returns>
    public UpdateTermsDocumentCommand ToCommand(TermsDocumentId termsDocumentId)
    {
        return new UpdateTermsDocumentCommand(
            termsDocumentId,
            Name,
            Description,
            RequiresSignature,
            Kind.ToDomain()
        );
    }
}
