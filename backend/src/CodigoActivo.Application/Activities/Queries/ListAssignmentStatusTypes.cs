using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record ListAssignmentStatusTypesQuery
    : IQuery<IReadOnlyList<AssignmentStatusTypeResponse>>;

public sealed class ListAssignmentStatusTypesQueryHandler(
    IAssignmentStatusTypeRepository statuses,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListAssignmentStatusTypesQuery, IReadOnlyList<AssignmentStatusTypeResponse>>
{
    public Task<IReadOnlyList<AssignmentStatusTypeResponse>> HandleAsync(
        ListAssignmentStatusTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "activities:assignment-status-types",
            () =>
                statuses
                    .Query()
                    .OrderBy(status => status.Name)
                    .Select(Projections.AssignmentStatusType),
            ct
        );
    }
}
