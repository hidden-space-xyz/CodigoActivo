using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.EventCategories;
using CodigoActivo.Application.EventCategories.Contracts;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve the category types assigned to past events.
/// </summary>
public sealed record GetPastEventCategoryTypesQuery
    : IQuery<IReadOnlyList<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the query to retrieve the category types assigned to past events.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetPastEventCategoryTypesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetPastEventCategoryTypesQuery, IReadOnlyList<EventCategoryTypeResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the category types assigned to past events.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event category type items.</returns>
    public Task<IReadOnlyList<EventCategoryTypeResponse>> HandleAsync(
        GetPastEventCategoryTypesQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(ct);
    }

    private Task<IReadOnlyList<EventCategoryTypeResponse>> FetchAsync(CancellationToken ct)
    {
        var today = clock.Today;
        return executor.ToListAsync(
            readStore
                .EventCategoryTypes.Where(c => c.Events.Any(ec => ec.Event.EventEndsAt < today))
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Id)
                .Select(EventCategoryProjections.EventCategoryType),
            ct
        );
    }
}
