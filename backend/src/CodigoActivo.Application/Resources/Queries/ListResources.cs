using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Resources.Queries;

public sealed record ListResourcesQuery(ResourceListQuery Filters)
    : IQuery<PagedResult<ResourceListItemResponse>>;

public sealed class ListResourcesQueryHandler(
    IResourceRepository resources,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
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

    public async Task<PagedResult<ResourceListItemResponse>> HandleAsync(
        ListResourcesQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("resources:list", query.Filters),
            token => new ValueTask<PagedResult<ResourceListItemResponse>>(
                FetchAsync(query.Filters, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.Resources],
            ct
        );
    }

    private Task<PagedResult<ResourceListItemResponse>> FetchAsync(
        ResourceListQuery query,
        CancellationToken ct
    )
    {
        var source = resources.Query().Select(Projections.ResourceListItem);

        if (query.ResourceTypeId is { } resourceTypeId)
        {
            source = source.Where(r => r.Type.Id == resourceTypeId);
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            var createdLower = LocalDayRange.LowerUtc(createdFrom, clock.TimeZone);
            source = source.Where(r => r.CreatedAt >= createdLower);
        }

        if (query.CreatedTo is { } createdTo)
        {
            var createdUpper = LocalDayRange.UpperExclusiveUtc(createdTo, clock.TimeZone);
            source = source.Where(r => r.CreatedAt < createdUpper);
        }

        source = source.WhereContains(r => r.Title, query.Title);
        source = source.WhereContains(r => r.Subtitle, query.Subtitle);
        source = source.WhereContains(r => r.Url, query.Url);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
