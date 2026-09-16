using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list activity role types.
/// </summary>
public sealed record ListActivityRoleTypesQuery : IQuery<IReadOnlyList<ActivityRoleTypeResponse>>;

/// <summary>
/// Executes the query to list activity role types.
/// </summary>
/// <param name="roleTypes">Repository used to persist and retrieve role types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListActivityRoleTypesQueryHandler(
    IActivityRoleTypeRepository roleTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListActivityRoleTypesQuery, IReadOnlyList<ActivityRoleTypeResponse>>
{
    /// <summary>
    /// Handles the request to list activity role types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity role type items.</returns>
    public Task<IReadOnlyList<ActivityRoleTypeResponse>> HandleAsync(
        ListActivityRoleTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:role-types",
            () => roleTypes.Query().OrderBy(role => role.Name).Select(Projections.ActivityRoleType),
            ct
        );
    }
}
