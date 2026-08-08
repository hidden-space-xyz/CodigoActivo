using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Resources.Queries;

public sealed record GetResourceByIdQuery(Guid ResourceId) : IQuery<Result<ResourceResponse>>;

public sealed class GetResourceByIdQueryHandler(
    IResourceRepository resources,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetResourceByIdQuery, Result<ResourceResponse>>
{
    public Task<Result<ResourceResponse>> HandleAsync(
        GetResourceByIdQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetEntityAsync(
            executor,
            $"resources:id:{query.ResourceId}",
            () =>
                resources.Query().Where(r => r.Id == query.ResourceId).Select(Projections.Resource),
            CacheTags.Resources,
            ErrorCode.ResourceNotFound,
            ct
        );
    }
}
