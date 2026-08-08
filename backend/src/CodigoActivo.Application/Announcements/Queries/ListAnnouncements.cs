using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Announcements.Queries;

public sealed record ListAnnouncementsQuery(AnnouncementListQuery Filters)
    : IQuery<PagedResult<AnnouncementListItemResponse>>;

public sealed class ListAnnouncementsQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
) : IQueryHandler<ListAnnouncementsQuery, PagedResult<AnnouncementListItemResponse>>
{
    private static readonly SortMap<AnnouncementListItemResponse> Sort =
        new SortMap<AnnouncementListItemResponse>()
            .Add("createdAt", a => a.CreatedAt)
            .Add("title", a => a.Title)
            .Add("subtitle", a => a.Subtitle)
            .Add("featured", a => a.Featured)
            .Default("-createdAt")
            .Tie(a => a.Id);

    public async Task<PagedResult<AnnouncementListItemResponse>> HandleAsync(
        ListAnnouncementsQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("announcements:list", query.Filters),
            token => new ValueTask<PagedResult<AnnouncementListItemResponse>>(
                FetchAsync(query.Filters, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
            ct
        );
    }

    private Task<PagedResult<AnnouncementListItemResponse>> FetchAsync(
        AnnouncementListQuery query,
        CancellationToken ct
    )
    {
        var source = announcements.Query().Select(Projections.AnnouncementListItem);

        if (query.Year is { } year)
        {
            var valid = year is >= 1 and <= 9999;
            var lower = valid
                ? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero)
                : DateTimeOffset.MaxValue;
            var upper =
                valid && year < 9999
                    ? new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    : DateTimeOffset.MaxValue;
            source = source.Where(a => a.CreatedAt >= lower && a.CreatedAt < upper);
        }

        if (query.Featured is { } featured)
        {
            source = source.Where(a => a.Featured == featured);
        }

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

        source = source.WhereContains(a => a.Title, query.Title);
        source = source.WhereContains(a => a.Subtitle, query.Subtitle);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
