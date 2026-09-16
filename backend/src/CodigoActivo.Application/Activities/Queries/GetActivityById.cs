using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve activity by identifier.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record GetActivityByIdQuery(Guid ActivityId) : IQuery<Result<ActivityResponse>>;

/// <summary>
/// Executes the query to retrieve activity by identifier.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetActivityByIdQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>>
{
    /// <summary>
    /// Handles the request to retrieve activity by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an activity on success, or an application error on failure.</returns>
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
