using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

public sealed record DeleteTermsDocumentCommand(Guid TermsDocumentId) : ICommand<Result>;

public sealed class DeleteTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IEventRepository events,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteTermsDocumentCommand, Result>
{
    public async Task<Result> HandleAsync(
        DeleteTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        var termsDocument = await termsDocuments.FindAsync(
            x => x.Id == command.TermsDocumentId,
            ct
        );
        if (termsDocument is null)
        {
            return Error.NotFound(ErrorCode.TermsDocumentNotFound);
        }

        if (
            await events.ExistsAsync(e => e.TermsDocumentId == command.TermsDocumentId, ct)
            || await events.HasTermsAcceptancesAsync(command.TermsDocumentId, ct)
        )
        {
            return Error.Conflict(ErrorCode.TermsDocumentInUse);
        }

        termsDocuments.Remove(termsDocument);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.TermsDocuments);

        var orphanCandidates = RichTextFileReferences.Extract(termsDocument.Description).ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
