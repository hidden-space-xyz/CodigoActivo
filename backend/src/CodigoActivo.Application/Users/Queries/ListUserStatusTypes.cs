using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users.Contracts;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to list user status types.
/// </summary>
public sealed record ListUserStatusTypesQuery
    : IQuery<IReadOnlyList<UserStatusTypeResponse>>,
        ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Catalog;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags { get; } = [CacheTags.Catalogs];

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        return "users:status-types";
    }
}

/// <summary>
/// Executes the query to list user status types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListUserStatusTypesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListUserStatusTypesQuery, IReadOnlyList<UserStatusTypeResponse>>
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
        return executor.ToListAsync(
            readStore
                .UserStatusTypes.OrderBy(type => type.Name)
                .Select(UserProjections.UserStatusType),
            ct
        );
    }
}
