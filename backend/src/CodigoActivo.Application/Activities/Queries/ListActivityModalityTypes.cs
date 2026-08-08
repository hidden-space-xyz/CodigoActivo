using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record ListActivityModalityTypesQuery
    : IQuery<IReadOnlyList<ActivityModalityTypeResponse>>;

public sealed class ListActivityModalityTypesQueryHandler(
    IActivityModalityTypeRepository modalityTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListActivityModalityTypesQuery, IReadOnlyList<ActivityModalityTypeResponse>>
{
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
