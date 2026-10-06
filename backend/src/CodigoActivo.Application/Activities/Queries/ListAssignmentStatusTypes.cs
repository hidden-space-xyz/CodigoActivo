using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list assignment status types.
/// </summary>
public sealed record ListAssignmentStatusTypesQuery
    : IQuery<IReadOnlyList<AssignmentStatusTypeResponse>>,
        ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Catalog;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags { get; } = [CacheTags.Catalogs];

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        return "activities:assignment-status-types";
    }
}

/// <summary>
/// Executes the query to list assignment status types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListAssignmentStatusTypesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
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
        return executor.ToListAsync(
            readStore
                .AssignmentStatusTypes.OrderBy(status => status.Name)
                .Select(ActivityProjections.AssignmentStatusType),
            ct
        );
    }
}
