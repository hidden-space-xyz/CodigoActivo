using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record GetActivityByIdQuery(Guid ActivityId) : IQuery<Result<ActivityResponse>>;

public sealed class GetActivityByIdQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>>
{
    public Task<Result<ActivityResponse>> HandleAsync(
        GetActivityByIdQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetEntityAsync(
            executor,
            $"activities:id:{query.ActivityId}",
            () =>
                activities
                    .Query()
                    .Where(a => a.Id == query.ActivityId)
                    .Select(Projections.Activity),
            CacheTags.Activities,
            ErrorCode.ActivityNotFound,
            ct
        );
    }
}
