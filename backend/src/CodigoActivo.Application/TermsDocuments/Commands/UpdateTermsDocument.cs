using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to update the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">Name, unique among terms documents.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
public sealed record UpdateTermsDocumentCommand(
    TermsDocumentId TermsDocumentId,
    [property: Required, MaxLength(120), NotBlank] string Name,
    [property: Required, RichText(AllowImages = false), MaxLength(262144)] string Description
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
public sealed class UpdateTermsDocumentCommandHandler(ITermsDocumentRepository termsDocuments)
    : ICommandHandler<UpdateTermsDocumentCommand, Result>
{
    /// <summary>
    /// Handles the request to update the terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var termsDocument = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (termsDocument is null)
        {
            return Error.NotFound(ApplicationErrorCode.TermsDocumentNotFound);
        }

        var name = command.Name.Trim();
        if (await termsDocuments.NameExistsAsync(name, command.TermsDocumentId, ct))
        {
            return Error.Conflict(ApplicationErrorCode.TermsDocumentNameAlreadyExists);
        }

        termsDocument.Rewrite(name, RichText.From(command.Description));
        return Result.Success();
    }
}
