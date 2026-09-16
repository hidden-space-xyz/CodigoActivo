using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Announcements.Queries;

/// <summary>
/// Carries the criteria used to list announcements.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListAnnouncementsQuery(AnnouncementListQuery Filters)
    : IQuery<PagedResult<AnnouncementListItemResponse>>;

/// <summary>
/// Executes the query to list announcements.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ListAnnouncementsQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    IClock clock
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

    /// <summary>
    /// Handles the request to list announcements.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged announcement list item.</returns>
    public Task<PagedResult<AnnouncementListItemResponse>> HandleAsync(
        ListAnnouncementsQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(query.Filters, ct);
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
