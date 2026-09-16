using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list activity modality types.
/// </summary>
public sealed record ListActivityModalityTypesQuery
    : IQuery<IReadOnlyList<ActivityModalityTypeResponse>>;

/// <summary>
/// Executes the query to list activity modality types.
/// </summary>
/// <param name="modalityTypes">Repository used to persist and retrieve modality types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListActivityModalityTypesQueryHandler(
    IActivityModalityTypeRepository modalityTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListActivityModalityTypesQuery, IReadOnlyList<ActivityModalityTypeResponse>>
{
    /// <summary>
    /// Handles the request to list activity modality types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity modality type items.</returns>
    public Task<IReadOnlyList<ActivityModalityTypeResponse>> HandleAsync(
        ListActivityModalityTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:modality-types",
            () =>
                modalityTypes
                    .Query()
                    .OrderBy(modality => modality.Name)
                    .Select(Projections.ActivityModalityType),
            ct
        );
    }
}
