using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Events.Contracts;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to list events.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventsQuery(EventListQuery Filters)
    : IQuery<PagedResult<EventListItemResponse>>;

/// <summary>
/// Executes the query to list events.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ListEventsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
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

    /// <summary>
    /// Handles the request to list events.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged event list item.</returns>
    public Task<PagedResult<EventListItemResponse>> HandleAsync(
        ListEventsQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(query.Filters, ct);
    }

    private async Task<PagedResult<EventListItemResponse>> FetchAsync(
        EventListQuery query,
        CancellationToken ct
    )
    {
        var now = clock.UtcNow;
        var today = clock.Today;
        var source = readStore.Events.Select(EventProjections.EventListItem);

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

        source = source.WhereContains(e => e.Title + " " + e.Subtitle, query.Search);
        source = source.WhereContains(e => e.Title, query.Title);
        source = source.WhereContains(e => e.Subtitle, query.Subtitle);

        source = Sort.Apply(source, query.Sort);
        var page = await executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
        return page with
        {
            Items = [.. page.Items.Select(item => EventStages.Stamp(item, now, today))],
        };
    }
}
