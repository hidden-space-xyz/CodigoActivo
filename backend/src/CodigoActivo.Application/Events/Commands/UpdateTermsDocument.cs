using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

public sealed record UpdateTermsDocumentCommand(
    Guid TermsDocumentId,
    UpdateTermsDocumentRequest Request
) : ICommand<Result<TermsDocumentResponse>>;

public sealed class UpdateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateTermsDocumentCommand, Result<TermsDocumentResponse>>
{
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
