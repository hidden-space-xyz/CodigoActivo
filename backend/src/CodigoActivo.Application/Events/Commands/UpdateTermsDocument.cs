using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to update the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record UpdateTermsDocumentCommand(
    Guid TermsDocumentId,
    UpdateTermsDocumentRequest Request
) : ICommand<Result<TermsDocumentResponse>>;

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
) : ICommandHandler<UpdateTermsDocumentCommand, Result<TermsDocumentResponse>>
{
    /// <summary>
    /// Handles the request to update the terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a terms document on success, or an application error on failure.</returns>
    public async Task<Result<TermsDocumentResponse>> HandleAsync(
        UpdateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var termsDocument = await termsDocuments.FindAsync(
            x => x.Id == command.TermsDocumentId,
            ct
        );
        if (termsDocument is null)
        {
            return Error.NotFound(ErrorCode.TermsDocumentNotFound);
        }

        var name = request.Name.Trim();
        if (
            await termsDocuments.ExistsAsync(
                x => x.Name == name && x.Id != command.TermsDocumentId,
                ct
            )
        )
        {
            return Error.Conflict(ErrorCode.TermsDocumentNameAlreadyExists);
        }

        var previousDescription = termsDocument.Description;

        termsDocument.Name = name;
        termsDocument.Description = request.Description;
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.TermsDocuments, CacheTags.Events);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, termsDocument.Description)
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return termsDocument.ToResponse();
    }
}
