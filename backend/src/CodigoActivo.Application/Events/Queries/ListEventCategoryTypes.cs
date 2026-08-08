using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

public sealed record ListEventCategoryTypesQuery(EventCategoryTypeListQuery Filters)
    : IQuery<PagedResult<EventCategoryTypeResponse>>;

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
