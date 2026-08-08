using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Partners.Commands;

public sealed record UpdatePartnerCommand(Guid PartnerId, UpdatePartnerRequest Request, Guid UserId)
    : ICommand<Result<PartnerResponse>>;

public sealed class UpdatePartnerCommandHandler(
    IPartnerRepository partners,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdatePartnerCommand, Result<PartnerResponse>>
{
    public async Task<Result<PartnerResponse>> HandleAsync(
        UpdatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var partner = await partners.FindAsync(p => p.Id == command.PartnerId, ct);
        if (partner is null)
        {
            return Error.NotFound(ErrorCode.PartnerNotFound);
        }

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);
        }

        var previousThumbnailId = partner.ThumbnailId;

        partner.Name = request.Name.Trim();
        partner.FromDate = request.FromDate!.Value;
        partner.Tier = request.Tier;
        partner.Web = request.Website.NormalizeOrNull();
        partner.ThumbnailId = request.ThumbnailId;
        partner.UpdatedAt = clock.UtcNow;
        partner.UpdatedBy = command.UserId;

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);

        if (previousThumbnailId != request.ThumbnailId)
        {
            await orphanCleaner.DeleteIfOrphanedAsync(previousThumbnailId, ct);
        }

        return partner.ToResponse();
    }
}
