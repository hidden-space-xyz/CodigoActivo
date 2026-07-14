using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcements,
    IFileRepository files,
    IFileService fileService,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    HybridCache cache,
    ICacheInvalidator cacheInvalidator
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

    public async Task<PagedResult<AnnouncementListItemResponse>> ListAsync(
        AnnouncementListQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("announcements:list", query),
            token => new ValueTask<PagedResult<AnnouncementListItemResponse>>(
                FetchListAsync(query, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
            ct
        );
    }

    private Task<PagedResult<AnnouncementListItemResponse>> FetchListAsync(
        AnnouncementListQuery query,
        CancellationToken ct
    )
    {
        var source = announcements.Query().Select(Projections.AnnouncementListItem);

        if (query.Year is { } year)
        {
            if (year is < 1 or > 9999)
            {
                source = source.Where(a => a.CreatedAt > DateTimeOffset.MaxValue);
            }
            else
            {
                var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                source =
                    year == 9999
                        ? source.Where(a => a.CreatedAt >= yearStart)
                        : source.Where(a =>
                            a.CreatedAt >= yearStart
                            && a.CreatedAt
                                < new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
                        );
            }
        }

        if (query.Featured is { } featured)
            source = source.Where(a => a.Featured == featured);
        if (query.CreatedFrom is { } createdFrom)
        {
            var createdLower = LocalDayRange.LowerUtc(createdFrom, clock.TimeZone);
            source = source.Where(a => a.CreatedAt >= createdLower);
        }

        if (query.CreatedTo is { } createdTo)
        {
            var createdUpper = LocalDayRange.UpperExclusiveUtc(createdTo, clock.TimeZone);
            source = source.Where(a => a.CreatedAt < createdUpper);
        }

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            source = source.Where(
                TextSearch.Contains<AnnouncementListItemResponse>(
                    a => a.Title,
                    TextSearch.Normalize(query.Title)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Subtitle))
        {
            source = source.Where(
                TextSearch.Contains<AnnouncementListItemResponse>(
                    a => a.Subtitle,
                    TextSearch.Normalize(query.Subtitle)
                )
            );
        }

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<AnnouncementResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await cache.GetOrCreateAsync(
            $"announcements:id:{id}",
            token => new ValueTask<AnnouncementResponse?>(
                executor.FirstOrDefaultAsync(
                    announcements.Query().Where(a => a.Id == id).Select(Projections.Announcement),
                    token
                )
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
            ct
        );
        return response is null
            ? (Result<AnnouncementResponse>)Error.NotFound(ErrorCode.AnnouncementNotFound)
            : (Result<AnnouncementResponse>)response;
    }

    public async Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "announcements:years",
            token => new ValueTask<IReadOnlyList<int>>(
                executor.ToListAsync(
                    announcements
                        .Query()
                        .Select(a => a.CreatedAt.Year)
                        .Distinct()
                        .OrderByDescending(year => year),
                    token
                )
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
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
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

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
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);
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
        if (announcement is null)
            return Error.NotFound(ErrorCode.AnnouncementNotFound);

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.AnnouncementThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

        var previousThumbnailId = announcement.ThumbnailId;
        var previousDescription = announcement.Description;

        announcement.Title = request.Title.Trim();
        announcement.Subtitle = request.Subtitle.Trim();
        announcement.Description = request.Description;
        announcement.ThumbnailId = request.ThumbnailId;
        announcement.UpdatedAt = clock.UtcNow;
        announcement.UpdatedBy = userId;

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, announcement.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
            orphanCandidates.Add(previousThumbnailId);
        await fileService.DeleteOrphanedAsync(orphanCandidates, ct);

        return announcement.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var announcement = await announcements.FindAsync(a => a.Id == id, ct);
        if (announcement is null)
            return Error.NotFound(ErrorCode.AnnouncementNotFound);

        announcements.Remove(announcement);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);

        var orphanCandidates = RichTextFileReferences
            .Extract(announcement.Description)
            .Append(announcement.ThumbnailId)
            .Distinct()
            .ToList();
        await fileService.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }

    public async Task<Result<AnnouncementResponse>> SetFeaturedAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (!await announcements.SetFeaturedAsync(id, ct))
            return Error.NotFound(ErrorCode.AnnouncementNotFound);

        await cacheInvalidator.InvalidateAsync(CacheTags.Announcements);
        return await GetByIdAsync(id, ct);
    }
}
