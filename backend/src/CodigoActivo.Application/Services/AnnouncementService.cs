using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IFileService fileService,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IAnnouncementService
{
    private static readonly SortMap<AnnouncementListItemResponse> Sort =
        new SortMap<AnnouncementListItemResponse>()
            .Add("createdAt", a => a.CreatedAt)
            .Add("title", a => a.Title)
            .Add("subtitle", a => a.Subtitle)
            .Add("featured", a => a.Featured)
            .Default("-createdAt")
            .Tie(a => a.Id);

    public Task<PagedResult<AnnouncementListItemResponse>> ListAsync(
        AnnouncementListQuery query,
        CancellationToken ct = default
    )
    {
        var source = announcements.Query().Select(Projections.AnnouncementListItem);

        if (query.Year is { } year) source = source.Where(a => a.CreatedAt.Year == year);
        if (query.Featured is { } featured) source = source.Where(a => a.Featured == featured);
        if (!string.IsNullOrWhiteSpace(query.Title))
            source = source.Where(
                TextSearch.Contains<AnnouncementListItemResponse>(
                    a => a.Title,
                    TextSearch.Normalize(query.Title)
                )
            );
        if (!string.IsNullOrWhiteSpace(query.Subtitle))
            source = source.Where(
                TextSearch.Contains<AnnouncementListItemResponse>(
                    a => a.Subtitle,
                    TextSearch.Normalize(query.Subtitle)
                )
            );

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<AnnouncementResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            announcements.Query().Where(a => a.Id == id).Select(Projections.Announcement),
            ct
        );
        if (response is null) return Error.NotFound(ErrorCode.AnnouncementNotFound);
        return response;
    }

    public async Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken ct = default)
    {
        return await executor.ToListAsync(
            announcements
                .Query()
                .Select(a => a.CreatedAt.Year)
                .Distinct()
                .OrderByDescending(year => year),
            ct
        );
    }

    public async Task<Result<AnnouncementResponse>> CreateAsync(
        CreateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.AnnouncementThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = userId,
        };
        await announcements.AddAsync(announcement, ct);
        await uow.SaveChangesAsync(ct);
        return announcement.ToResponse();
    }

    public async Task<Result<AnnouncementResponse>> UpdateAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        if (announcement is null) return Error.NotFound(ErrorCode.AnnouncementNotFound);

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.AnnouncementThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var previousThumbnailId = announcement.ThumbnailId;
        var previousDescription = announcement.Description;

        announcement.Title = request.Title.Trim();
        announcement.Subtitle = request.Subtitle.Trim();
        announcement.Description = request.Description;
        announcement.ThumbnailId = request.ThumbnailId;
        announcement.UpdatedAt = clock.UtcNow;
        announcement.UpdatedBy = userId;

        await uow.SaveChangesAsync(ct);

        if (previousThumbnailId != request.ThumbnailId)
            await fileService.DeleteIfOrphanedAsync(previousThumbnailId, ct);

        foreach (var fileId in RichTextFileReferences.ExtractRemoved(previousDescription, announcement.Description))
            await fileService.DeleteIfOrphanedAsync(fileId, ct);

        return announcement.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        if (announcement is null) return Error.NotFound(ErrorCode.AnnouncementNotFound);

        announcements.Remove(announcement);
        await uow.SaveChangesAsync(ct);

        await fileService.DeleteIfOrphanedAsync(announcement.ThumbnailId, ct);
        foreach (var fileId in RichTextFileReferences.Extract(announcement.Description))
            await fileService.DeleteIfOrphanedAsync(fileId, ct);

        return Result.Success();
    }

    public async Task<Result<AnnouncementResponse>> SetFeaturedAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (!await announcements.SetFeaturedAsync(id, ct))
            return Error.NotFound(ErrorCode.AnnouncementNotFound);

        return await GetByIdAsync(id, ct);
    }
}