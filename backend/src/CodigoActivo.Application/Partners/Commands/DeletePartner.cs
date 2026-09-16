using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to delete the partner.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
public sealed record DeletePartnerCommand(Guid PartnerId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the partner.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeletePartnerCommandHandler(
    IPartnerRepository partners,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeletePartnerCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeletePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var partner = await partners.FindAsync(p => p.Id == command.PartnerId, ct);
        if (partner is null)
        {
            return Error.NotFound(ErrorCode.PartnerNotFound);
        }

        partners.Remove(partner);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);

        await orphanCleaner.DeleteIfOrphanedAsync(partner.ThumbnailId, ct);
        return Result.Success();
    }
}
