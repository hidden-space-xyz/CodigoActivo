using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to create a terms document.
/// </summary>
/// <param name="Name">Name, unique among terms documents.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
public sealed record CreateTermsDocumentCommand(
    [property: Required, MaxLength(120), NotBlank] string Name,
    [property: Required, RichText(AllowImages = false), MaxLength(262144)] string Description
) : ICommand<Result<TermsDocumentId>>;

/// <summary>
/// Executes the command to create a terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
public sealed class CreateTermsDocumentCommandHandler(ITermsDocumentRepository termsDocuments)
    : ICommandHandler<CreateTermsDocumentCommand, Result<TermsDocumentId>>
{
    /// <summary>
    /// Handles the request to create a terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<TermsDocumentId>> HandleAsync(
        CreateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = command.Name.Trim();
        if (await termsDocuments.NameExistsAsync(name, ct: ct))
        {
            return Error.Conflict(ApplicationErrorCode.TermsDocumentNameAlreadyExists);
        }

        var termsDocument = TermsDocument.Create(name, RichText.From(command.Description));
        await termsDocuments.AddAsync(termsDocument, ct);
        return termsDocument.Id;
    }
}
