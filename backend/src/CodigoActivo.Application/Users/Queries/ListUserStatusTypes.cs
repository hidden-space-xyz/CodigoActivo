using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users.Contracts;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to list user status types.
/// </summary>
public sealed record ListUserStatusTypesQuery : IQuery<IReadOnlyList<UserStatusTypeResponse>>;

/// <summary>
/// Executes the query to list user status types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListUserStatusTypesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListUserStatusTypesQuery, IReadOnlyList<UserStatusTypeResponse>>
{
    /// <summary>
    /// Handles the request to list user status types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user status type items.</returns>
    public Task<IReadOnlyList<UserStatusTypeResponse>> HandleAsync(
        ListUserStatusTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "users:status-types",
            () =>
                readStore
                    .UserStatusTypes.OrderBy(type => type.Name)
                    .Select(UserProjections.UserStatusType),
            ct
        );
    }
}
