using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve past event years.
/// </summary>
public sealed record GetPastEventYearsQuery : IQuery<IReadOnlyList<int>>;

/// <summary>
/// Executes the query to retrieve past event years.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetPastEventYearsQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
) : IQueryHandler<GetPastEventYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request to retrieve past event years.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching int items.</returns>
    public async Task<IReadOnlyList<int>> HandleAsync(
        GetPastEventYearsQuery query,
        CancellationToken ct = default
    )
    {
        var dayKey = clock.Today.DayNumber.ToString(CultureInfo.InvariantCulture);
        return await cache.GetOrCreateAsync(
            $"events:past-years:{dayKey}",
            token => new ValueTask<IReadOnlyList<int>>(FetchAsync(token)),
            CachePolicies.PublicContent,
            [CacheTags.Events],
            ct
        );
    }

    private Task<IReadOnlyList<int>> FetchAsync(CancellationToken ct)
    {
        var today = clock.Today;
        return executor.ToListAsync(
            events
                .Query()
                .Where(e => e.EventEndsAt < today)
                .Select(e => e.EventStartsAt.Year)
                .Distinct()
                .OrderByDescending(year => year),
            ct
        );
    }
}
