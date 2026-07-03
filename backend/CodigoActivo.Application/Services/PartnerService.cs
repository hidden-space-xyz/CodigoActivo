using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class PartnerService(IPartnerRepository partners, IFileRepository files, IUnitOfWork uow)
    : IPartnerService
{
    public IQueryable<PartnerResponse> Query()
    {
        return partners.Query().Select(Projections.Partner);
    }

    public async Task<Result<PartnerResponse>> CreateAsync(
        CreatePartnerRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var partner = new Partner
        {
            Name = request.Name.Trim(),
            FromDate = request.FromDate!.Value,
            Tier = request.Tier,
            Web = request.Website.NormalizeOrNull(),
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };
        await partners.AddAsync(partner, ct);
        await uow.SaveChangesAsync(ct);
        return partner.ToResponse();
    }

    public async Task<Result<PartnerResponse>> UpdateAsync(
        Guid id,
        UpdatePartnerRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var partner = await partners.FindAsync(p => p.Id == id, ct);
        if (partner is null) return Error.NotFound(ErrorCode.PartnerNotFound);

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

        partner.Name = request.Name.Trim();
        partner.FromDate = request.FromDate!.Value;
        partner.Tier = request.Tier;
        partner.Web = request.Website.NormalizeOrNull();
        partner.ThumbnailId = request.ThumbnailId;
        partner.UpdatedAt = DateTimeOffset.UtcNow;
        partner.UpdatedBy = userId;

        partners.Update(partner);
        await uow.SaveChangesAsync(ct);
        return partner.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await partners.ExistsAsync(p => p.Id == id, ct)) return Error.NotFound(ErrorCode.PartnerNotFound);

        await partners.RemoveAsync(p => p.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
            return Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);

        return Result.Success();
    }
}