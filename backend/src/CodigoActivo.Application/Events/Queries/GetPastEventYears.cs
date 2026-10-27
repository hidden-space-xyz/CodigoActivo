using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the request for the years in which past events started.
/// </summary>
public sealed record GetPastEventYearsQuery : IQuery<IReadOnlyList<int>>;

/// <summary>
/// Executes the query for the years in which past events, those whose last day is before today,
/// started.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetPastEventYearsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetPastEventYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request for the years in which past events started.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains each year once, the most recent first.</returns>
    public Task<IReadOnlyList<int>> HandleAsync(
        GetPastEventYearsQuery query,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;
        return executor.ToListAsync(
            readStore
                .Events.Where(EventProjections.EndedBy(today))
                .Select(e => e.EventStartsAt.Year)
                .Distinct()
                .OrderByDescending(year => year),
            ct
        );
    }
}
