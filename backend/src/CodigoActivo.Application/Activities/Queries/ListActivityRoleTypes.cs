using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list activity role types.
/// </summary>
public sealed record ListActivityRoleTypesQuery : IQuery<IReadOnlyList<ActivityRoleTypeResponse>>;

/// <summary>
/// Executes the query to list activity role types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListActivityRoleTypesQueryHandler(
    IReadStore readStore,
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
            () =>
                readStore
                    .ActivityRoleTypes.OrderBy(role => role.Name)
                    .Select(ActivityProjections.ActivityRoleType),
            ct
        );
    }
}
