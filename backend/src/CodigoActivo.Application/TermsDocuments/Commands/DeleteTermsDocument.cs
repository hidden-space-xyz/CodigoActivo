using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to delete the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
public sealed record DeleteTermsDocumentCommand(TermsDocumentId TermsDocumentId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="termsAcceptances">Repository used to persist and retrieve terms acceptances.</param>
public sealed class DeleteTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IEventRepository events,
    IEventTermsAcceptanceRepository termsAcceptances
) : ICommandHandler<DeleteTermsDocumentCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var termsDocument = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (termsDocument is null)
        {
            return Error.NotFound(ApplicationErrorCode.TermsDocumentNotFound);
        }

        if (
            await events.LinksTermsDocumentAsync(command.TermsDocumentId, ct)
            || await termsAcceptances.AnyForDocumentAsync(command.TermsDocumentId, ct)
        )
        {
            return Error.Conflict(ApplicationErrorCode.TermsDocumentInUse);
        }

        termsDocument.Delete();
        termsDocuments.Remove(termsDocument);
        return Result.Success();
    }
}
