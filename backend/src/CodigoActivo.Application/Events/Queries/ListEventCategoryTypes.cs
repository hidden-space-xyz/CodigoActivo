using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to list event category types.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventCategoryTypesQuery(EventCategoryTypeListQuery Filters)
    : IQuery<PagedResult<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the query to list event category types.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListEventCategoryTypesQueryHandler(
    IEventCategoryTypeRepository categoryTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListEventCategoryTypesQuery, PagedResult<EventCategoryTypeResponse>>
{
    private static readonly SortMap<EventCategoryTypeResponse> Sort =
        new SortMap<EventCategoryTypeResponse>()
            .Add("name", c => c.Name)
            .Add("color", c => c.Color)
            .Default("name")
            .Tie(c => c.Id);

    /// <summary>
    /// Handles the request to list event category types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged event category type.</returns>
    public async Task<PagedResult<EventCategoryTypeResponse>> HandleAsync(
        ListEventCategoryTypesQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("events:category-types", query.Filters),
            token => new ValueTask<PagedResult<EventCategoryTypeResponse>>(
                FetchAsync(query.Filters, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.EventCategoryTypes],
            ct
        );
    }

    private Task<PagedResult<EventCategoryTypeResponse>> FetchAsync(
        EventCategoryTypeListQuery query,
        CancellationToken ct
    )
    {
        var source = categoryTypes.Query().Select(Projections.EventCategoryType);

        source = source.WhereContains(c => c.Name, query.Name);
        source = source.WhereContains(c => c.Color, query.Color);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
