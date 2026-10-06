using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list activity modality types.
/// </summary>
public sealed record ListActivityModalityTypesQuery
    : IQuery<IReadOnlyList<ActivityModalityTypeResponse>>,
        ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Catalog;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags { get; } = [CacheTags.Catalogs];

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        return "activities:modality-types";
    }
}

/// <summary>
/// Executes the query to list activity modality types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListActivityModalityTypesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
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
        return executor.ToListAsync(
            readStore
                .ActivityModalityTypes.OrderBy(modality => modality.Name)
                .Select(ActivityProjections.ActivityModalityType),
            ct
        );
    }
}
