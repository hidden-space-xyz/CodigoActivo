using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Partners.Commands;

public sealed record DeletePartnerCommand(Guid PartnerId) : ICommand<Result>;

public sealed class DeletePartnerCommandHandler(
    IPartnerRepository partners,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeletePartnerCommand, Result>
{
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
