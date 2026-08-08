using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Resources.Queries;

public sealed record ListResourceTypesQuery : IQuery<IReadOnlyList<ResourceTypeResponse>>;

public sealed class ListResourceTypesQueryHandler(
    IResourceTypeRepository resourceTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListResourceTypesQuery, IReadOnlyList<ResourceTypeResponse>>
{
    public Task<IReadOnlyList<ResourceTypeResponse>> HandleAsync(
        ListResourceTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "resources:types",
            () => resourceTypes.Query().OrderBy(type => type.Name).Select(Projections.ResourceType),
            ct
        );
    }
}
