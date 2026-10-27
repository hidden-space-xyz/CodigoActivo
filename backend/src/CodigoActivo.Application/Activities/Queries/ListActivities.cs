using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list activities.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListActivitiesQuery(ActivityListQuery Filters)
    : IQuery<PagedResult<ActivityResponse>>;

/// <summary>
/// Executes the query to list activities.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ListActivitiesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<ListActivitiesQuery, PagedResult<ActivityResponse>>
{
    private static readonly SortMap<ActivityResponse> Sort = new SortMap<ActivityResponse>()
        .Add("activityStartsAt", a => a.ActivityStartsAt)
        .Add("activityEndsAt", a => a.ActivityEndsAt)
        .Add("title", a => a.Title)
        .Add("modalityName", a => a.ModalityName)
        .Add("location", a => a.Location)
        .Add("createdAt", a => a.CreatedAt)
        .Default("activityStartsAt")
        .Tie(a => a.Id);

    /// <summary>
    /// Handles the request to list activities.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the requested page of activities, by start unless the sort
    /// names <c>activityEndsAt</c>, <c>title</c>, <c>modalityName</c>, <c>location</c> or
    /// <c>createdAt</c>.
    /// </returns>
    public Task<PagedResult<ActivityResponse>> HandleAsync(
        ListActivitiesQuery query,
        CancellationToken ct = default
    )
    {
        var filters = query.Filters;
        var source = readStore.Activities.Select(ActivityProjections.Activity);

        if (filters.EventId is { } eventId)
        {
            source = source.Where(a => a.EventId == eventId);
        }

        if (filters.ModalityTypeId is { } modalityTypeId)
        {
            source = source.Where(a => a.ModalityId == modalityTypeId);
        }

        if (filters.ActivityDateFrom is { } activityDateFrom)
        {
            var activityLower = LocalDayRange.LowerUtc(activityDateFrom, clock.TimeZone);
            source = source.Where(a => a.ActivityEndsAt >= activityLower);
        }

        if (filters.ActivityDateTo is { } activityDateTo)
        {
            var activityUpper = LocalDayRange.UpperExclusiveUtc(activityDateTo, clock.TimeZone);
            source = source.Where(a => a.ActivityStartsAt < activityUpper);
        }

        source = source.WhereContains(a => a.Title, filters.Title);
        source = source.WhereContains(a => a.Location, filters.Location);

        source = Sort.Apply(source, filters.Sort);
        return executor.ToPagedAsync(source, filters.Page, filters.PageSize, ct);
    }
}
