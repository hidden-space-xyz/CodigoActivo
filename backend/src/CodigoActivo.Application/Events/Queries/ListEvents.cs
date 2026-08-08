using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

public sealed record ListEventsQuery(EventListQuery Filters)
    : IQuery<PagedResult<EventListItemResponse>>;

public sealed class ListEventsQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
) : IQueryHandler<ListEventsQuery, PagedResult<EventListItemResponse>>
{
    private static readonly SortMap<EventListItemResponse> Sort =
        new SortMap<EventListItemResponse>()
            .Add("eventStartsAt", e => e.EventStartsAt)
            .Add("eventEndsAt", e => e.EventEndsAt)
            .Add("signupStartsAt", e => e.SignupStartsAt)
            .Add("signupEndsAt", e => e.SignupEndsAt)
            .Add("createdAt", e => e.CreatedAt)
            .Add("title", e => e.Title)
            .Add("subtitle", e => e.Subtitle)
            .Add("featured", e => e.Featured)
            .Add("categories", e => e.Categories.Min(c => c.Name))
            .Default("eventStartsAt")
            .Tie(e => e.Id);

    public async Task<PagedResult<EventListItemResponse>> HandleAsync(
        ListEventsQuery query,
        CancellationToken ct = default
    )
    {
        var dayKey = clock.Today.DayNumber.ToString(CultureInfo.InvariantCulture);
        return await cache.GetOrCreateAsync(
            CacheKeys.For($"events:list:{dayKey}", query.Filters),
            token => new ValueTask<PagedResult<EventListItemResponse>>(
                FetchAsync(query.Filters, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.Events],
            ct
        );
    }

    private Task<PagedResult<EventListItemResponse>> FetchAsync(
        EventListQuery query,
        CancellationToken ct
    )
    {
        var today = clock.Today;
        var source = events.Query().Select(Projections.EventListItem);

        source = query.Scope switch
        {
            EventScope.Upcoming => source.Where(e => e.EventEndsAt >= today),
            EventScope.Past => source.Where(e => e.EventEndsAt < today),
            _ => source,
        };

        if (query.Year is { } year)
        {
            var valid = year is >= 1 and <= 9999;
            var lower = valid ? new DateOnly(year, 1, 1) : DateOnly.MaxValue;
            var upper = valid ? new DateOnly(year, 12, 31) : DateOnly.MinValue;
            source = source.Where(e => e.EventStartsAt >= lower && e.EventStartsAt <= upper);
        }

        if (query.Featured is { } featured)
        {
            source = source.Where(e => e.Featured == featured);
        }

        if (query.CategoryTypeId is { } categoryTypeId)
        {
            source = source.Where(e => e.Categories.Any(c => c.CategoryTypeId == categoryTypeId));
        }

        if (query.EventDateFrom is { } eventDateFrom)
        {
            source = source.Where(e => e.EventEndsAt >= eventDateFrom);
        }

        if (query.EventDateTo is { } eventDateTo)
        {
            source = source.Where(e => e.EventStartsAt <= eventDateTo);
        }

        if (query.SignupFrom is { } signupFrom)
        {
            var signupLower = LocalDayRange.LowerUtc(signupFrom, clock.TimeZone);
            source = source.Where(e => e.SignupEndsAt >= signupLower);
        }

        if (query.SignupTo is { } signupTo)
        {
            var signupUpper = LocalDayRange.UpperExclusiveUtc(signupTo, clock.TimeZone);
            source = source.Where(e => e.SignupStartsAt < signupUpper);
        }

        source = source.WhereContains(e => e.Title, query.Title);
        source = source.WhereContains(e => e.Subtitle, query.Subtitle);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
