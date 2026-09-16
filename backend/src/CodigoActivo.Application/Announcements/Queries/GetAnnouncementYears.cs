using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Announcements.Queries;

/// <summary>
/// Carries the criteria used to retrieve announcement years.
/// </summary>
public sealed record GetAnnouncementYearsQuery : IQuery<IReadOnlyList<int>>;

/// <summary>
/// Executes the query to retrieve announcement years.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetAnnouncementYearsQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetAnnouncementYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request to retrieve announcement years.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching int items.</returns>
    public async Task<IReadOnlyList<int>> HandleAsync(
        GetAnnouncementYearsQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            "announcements:years",
            token => new ValueTask<IReadOnlyList<int>>(
                executor.ToListAsync(
                    announcements
                        .Query()
                        .Select(a => a.CreatedAt.Year)
                        .Distinct()
                        .OrderByDescending(year => year),
                    token
                )
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
            ct
        );
    }
}
