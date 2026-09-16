using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

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
public sealed class GetPastEventYearsQueryHandler(
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetPastEventYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request to retrieve past event years.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching int items.</returns>
    public Task<IReadOnlyList<int>> HandleAsync(
        GetPastEventYearsQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(ct);
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
