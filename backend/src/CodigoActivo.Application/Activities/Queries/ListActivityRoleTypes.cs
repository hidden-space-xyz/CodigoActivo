using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record ListActivityRoleTypesQuery : IQuery<IReadOnlyList<ActivityRoleTypeResponse>>;

public sealed class ListActivityRoleTypesQueryHandler(
    IActivityRoleTypeRepository roleTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListActivityRoleTypesQuery, IReadOnlyList<ActivityRoleTypeResponse>>
{
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
