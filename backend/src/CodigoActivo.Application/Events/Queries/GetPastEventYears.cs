using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Events.Queries;

public sealed record GetPastEventYearsQuery : IQuery<IReadOnlyList<int>>;

public sealed class GetPastEventYearsQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
) : IQueryHandler<GetPastEventYearsQuery, IReadOnlyList<int>>
{
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
