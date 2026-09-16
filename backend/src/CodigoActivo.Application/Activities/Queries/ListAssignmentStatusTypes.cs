using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list assignment status types.
/// </summary>
public sealed record ListAssignmentStatusTypesQuery
    : IQuery<IReadOnlyList<AssignmentStatusTypeResponse>>;

/// <summary>
/// Executes the query to list assignment status types.
/// </summary>
/// <param name="statuses">Repository used to persist and retrieve statuses.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListAssignmentStatusTypesQueryHandler(
    IAssignmentStatusTypeRepository statuses,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListAssignmentStatusTypesQuery, IReadOnlyList<AssignmentStatusTypeResponse>>
{
    /// <summary>
    /// Handles the request to list assignment status types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching assignment status type items.</returns>
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
