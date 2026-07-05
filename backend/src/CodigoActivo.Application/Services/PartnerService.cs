using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class PartnerService(
    IPartnerRepository partners,
    IFileRepository files,
    IFileService fileService,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IPartnerService
{
    private static readonly SortMap<PartnerResponse> Sort = new SortMap<PartnerResponse>()
        .Add("name", p => p.Name)
        .Add("tier", p => p.Tier)
        .Add("website", p => p.Website)
        .Add("fromDate", p => p.FromDate)
        .Add("createdAt", p => p.CreatedAt)
        .Default("tier", "-fromDate")
        .Tie(p => p.Id);

    public Task<PagedResult<PartnerResponse>> ListAsync(
        PartnerListQuery query,
        CancellationToken ct = default
    )
    {
        var source = partners.Query().Select(Projections.Partner);

        if (query.Tier is { } tier) source = source.Where(p => p.Tier == tier);
        if (!string.IsNullOrWhiteSpace(query.Name))
            source = source.Where(
                TextSearch.Contains<PartnerResponse>(p => p.Name, TextSearch.Normalize(query.Name))
            );
        if (!string.IsNullOrWhiteSpace(query.Website))
            source = source.Where(
                TextSearch.Contains<PartnerResponse>(
                    p => p.Website,
                    TextSearch.Normalize(query.Website)
                )
            );

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<PartnerResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            partners.Query().Where(p => p.Id == id).Select(Projections.Partner),
            ct
        );
        if (response is null) return Error.NotFound(ErrorCode.PartnerNotFound);
        return response;
    }

    public async Task<Result<PartnerResponse>> CreateAsync(
        CreatePartnerRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.PartnerThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var partner = new Partner
        {
            Name = request.Name.Trim(),
            FromDate = request.FromDate!.Value,
            Tier = request.Tier,
            Web = request.Website.NormalizeOrNull(),
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
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

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.PartnerThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var previousThumbnailId = partner.ThumbnailId;

        partner.Name = request.Name.Trim();
        partner.FromDate = request.FromDate!.Value;
        partner.Tier = request.Tier;
        partner.Web = request.Website.NormalizeOrNull();
        partner.ThumbnailId = request.ThumbnailId;
        partner.UpdatedAt = clock.UtcNow;
        partner.UpdatedBy = userId;

        await uow.SaveChangesAsync(ct);

        if (previousThumbnailId != request.ThumbnailId)
            await fileService.DeleteIfOrphanedAsync(previousThumbnailId, ct);

        return partner.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var partner = await partners.FindAsync(p => p.Id == id, ct);
        if (partner is null) return Error.NotFound(ErrorCode.PartnerNotFound);

        partners.Remove(partner);
        await uow.SaveChangesAsync(ct);

        await fileService.DeleteIfOrphanedAsync(partner.ThumbnailId, ct);
        return Result.Success();
    }
}