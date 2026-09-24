using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.News.Queries;

/// <summary>
/// Carries the criteria used to list news items.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListNewsQuery(NewsListQuery Filters)
    : IQuery<PagedResult<NewsListItemResponse>>;

/// <summary>
/// Executes the query to list news items.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ListNewsQueryHandler(
    INewsItemRepository news,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<ListNewsQuery, PagedResult<NewsListItemResponse>>
{
    private static readonly SortMap<NewsListItemResponse> Sort = new SortMap<NewsListItemResponse>()
        .Add("createdAt", a => a.CreatedAt)
        .Add("title", a => a.Title)
        .Add("subtitle", a => a.Subtitle)
        .Add("featured", a => a.Featured)
        .Default("-createdAt")
        .Tie(a => a.Id);

    /// <summary>
    /// Handles the request to list news items.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged news list item.</returns>
    public Task<PagedResult<NewsListItemResponse>> HandleAsync(
        ListNewsQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(query.Filters, ct);
    }

    private Task<PagedResult<NewsListItemResponse>> FetchAsync(
        NewsListQuery query,
        CancellationToken ct
    )
    {
        var source = news.Query().Select(Projections.NewsListItem);

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

        source = source.WhereContains(a => a.Title + " " + a.Subtitle, query.Search);
        source = source.WhereContains(a => a.Title, query.Title);
        source = source.WhereContains(a => a.Subtitle, query.Subtitle);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
