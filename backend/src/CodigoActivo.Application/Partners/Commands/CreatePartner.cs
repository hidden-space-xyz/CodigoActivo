using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Partners.Commands;

public sealed record CreatePartnerCommand(CreatePartnerRequest Request, Guid UserId)
    : ICommand<Result<PartnerResponse>>;

public sealed class CreatePartnerCommandHandler(
    IPartnerRepository partners,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreatePartnerCommand, Result<PartnerResponse>>
{
    public async Task<Result<PartnerResponse>> HandleAsync(
        CreatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);
        }

        var partner = new Partner
        {
            Name = request.Name.Trim(),
            FromDate = request.FromDate!.Value,
            Tier = request.Tier,
            Web = request.Website.NormalizeOrNull(),
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        await partners.AddAsync(partner, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Partners);
        return partner.ToResponse();
    }
}
