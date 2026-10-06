using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Resources.Contracts;

namespace CodigoActivo.Application.Resources.Queries;

/// <summary>
/// Carries the criteria used to list resource types.
/// </summary>
public sealed record ListResourceTypesQuery
    : IQuery<IReadOnlyList<ResourceTypeResponse>>,
        ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Catalog;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags { get; } = [CacheTags.Catalogs];

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        return "resources:types";
    }
}

/// <summary>
/// Executes the query to list resource types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListResourceTypesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListResourceTypesQuery, IReadOnlyList<ResourceTypeResponse>>
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
        return executor.ToListAsync(
            readStore
                .ResourceTypes.OrderBy(type => type.Name)
                .Select(ResourceProjections.ResourceType),
            ct
        );
    }
}
