using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Resources.Queries;

/// <summary>
/// Carries the criteria used to list resource types.
/// </summary>
public sealed record ListResourceTypesQuery : IQuery<IReadOnlyList<ResourceTypeResponse>>;

/// <summary>
/// Executes the query to list resource types.
/// </summary>
/// <param name="resourceTypes">Repository used to persist and retrieve resource types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListResourceTypesQueryHandler(
    IResourceTypeRepository resourceTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListResourceTypesQuery, IReadOnlyList<ResourceTypeResponse>>
{
    /// <summary>
    /// Handles the request to list resource types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching resource type items.</returns>
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
