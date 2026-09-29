using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;

namespace CodigoActivo.Application.News.Queries;

/// <summary>
/// Carries the criteria used to retrieve news years.
/// </summary>
public sealed record GetNewsYearsQuery : IQuery<IReadOnlyList<int>>;

/// <summary>
/// Executes the query to retrieve news years.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetNewsYearsQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetNewsYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request to retrieve news years.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching int items.</returns>
    public Task<IReadOnlyList<int>> HandleAsync(
        GetNewsYearsQuery query,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
            readStore.News.Select(a => a.CreatedAt.Year).Distinct().OrderByDescending(year => year),
            ct
        );
    }
}
