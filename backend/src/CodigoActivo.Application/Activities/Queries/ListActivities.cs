using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

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
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListActivitiesQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
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
    /// <returns>A task whose result contains a paged activity.</returns>
    public async Task<PagedResult<ActivityResponse>> HandleAsync(
        ListActivitiesQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("activities:list", query.Filters),
            token => new ValueTask<PagedResult<ActivityResponse>>(FetchAsync(query.Filters, token)),
            CachePolicies.PublicContent,
            [CacheTags.Activities],
            ct
        );
    }

    private Task<PagedResult<ActivityResponse>> FetchAsync(
        ActivityListQuery query,
        CancellationToken ct
    )
    {
        var source = activities.Query().Select(Projections.Activity);

        if (query.EventId is { } eventId)
        {
            source = source.Where(a => a.EventId == eventId);
        }

        if (query.ModalityTypeId is { } modalityTypeId)
        {
            source = source.Where(a => a.ModalityId == modalityTypeId);
        }

        if (query.ActivityDateFrom is { } activityDateFrom)
        {
            var activityLower = LocalDayRange.LowerUtc(activityDateFrom, clock.TimeZone);
            source = source.Where(a => a.ActivityEndsAt >= activityLower);
        }

        if (query.ActivityDateTo is { } activityDateTo)
        {
            var activityUpper = LocalDayRange.UpperExclusiveUtc(activityDateTo, clock.TimeZone);
            source = source.Where(a => a.ActivityStartsAt < activityUpper);
        }

        source = source.WhereContains(a => a.Title, query.Title);
        source = source.WhereContains(a => a.Location, query.Location);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
