using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to delete the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
public sealed record DeleteTermsDocumentCommand(Guid TermsDocumentId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="termsAcceptances">Repository of the terms decisions people took.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class DeleteTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IEventRepository events,
    IEventTermsAcceptanceRepository termsAcceptances,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow
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
        var termsDocument = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (termsDocument is null)
        {
            return Error.NotFound(ErrorCode.TermsDocumentNotFound);
        }

        if (
            await events.LinksTermsDocumentAsync(command.TermsDocumentId, ct)
            || await termsAcceptances.AnyForDocumentAsync(command.TermsDocumentId, ct)
        )
        {
            return Error.Conflict(ErrorCode.TermsDocumentInUse);
        }

        termsDocuments.Remove(termsDocument);
        await uow.SaveChangesAsync(ct);

        var orphanCandidates = RichTextFileReferences.Extract(termsDocument.Description).ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
