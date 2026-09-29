using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to update the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record UpdateTermsDocumentCommand(
    Guid TermsDocumentId,
    UpdateTermsDocumentRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateTermsDocumentCommand, Result>
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
        var request = command.Request;

        var termsDocument = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (termsDocument is null)
        {
            return Error.NotFound(ErrorCode.TermsDocumentNotFound);
        }

        var name = request.Name.Trim();
        if (await termsDocuments.NameExistsAsync(name, command.TermsDocumentId, ct))
        {
            return Error.Conflict(ErrorCode.TermsDocumentNameAlreadyExists);
        }

        var previousDescription = termsDocument.Description;

        termsDocument.Rewrite(name, request.Description);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Events);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, termsDocument.Description)
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
