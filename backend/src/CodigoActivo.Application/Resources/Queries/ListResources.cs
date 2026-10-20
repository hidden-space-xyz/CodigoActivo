using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Resources.Queries;

/// <summary>
/// Carries the criteria used to list resources.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListResourcesQuery(ResourceListQuery Filters)
    : IQuery<PagedResult<ResourceListItemResponse>>;

/// <summary>
/// Executes the query to list resources.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ListResourcesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<ListResourcesQuery, PagedResult<ResourceListItemResponse>>
{
    private static readonly SortMap<ResourceListItemResponse> Sort =
        new SortMap<ResourceListItemResponse>()
            .Add("createdAt", r => r.CreatedAt)
            .Add("title", r => r.Title)
            .Add("subtitle", r => r.Subtitle)
            .Add("type", r => r.Type.Name)
            .Add("url", r => r.Url)
            .Default("-createdAt")
            .Tie(r => r.Id);

    /// <summary>
    /// Handles the request to list resources.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged resource list item.</returns>
    public Task<PagedResult<ResourceListItemResponse>> HandleAsync(
        ListResourcesQuery query,
        CancellationToken ct = default
    )
    {
        var filters = query.Filters;
        var source = readStore.Resources.Select(ResourceProjections.ResourceListItem);

        if (filters.ResourceTypeId is { } resourceTypeId)
        {
            source = source.Where(r => r.Type.Id == resourceTypeId);
        }

        if (filters.CreatedFrom is { } createdFrom)
        {
            var createdLower = LocalDayRange.LowerUtc(createdFrom, clock.TimeZone);
            source = source.Where(r => r.CreatedAt >= createdLower);
        }

        if (filters.CreatedTo is { } createdTo)
        {
            var createdUpper = LocalDayRange.UpperExclusiveUtc(createdTo, clock.TimeZone);
            source = source.Where(r => r.CreatedAt < createdUpper);
        }

        source = source.WhereContains(r => r.Title + " " + r.Subtitle, filters.Search);
        source = source.WhereContains(r => r.Title, filters.Title);
        source = source.WhereContains(r => r.Subtitle, filters.Subtitle);
        source = source.WhereContains(r => r.Url, filters.Url);

        source = Sort.Apply(source, filters.Sort);
        return executor.ToPagedAsync(source, filters.Page, filters.PageSize, ct);
    }
}
