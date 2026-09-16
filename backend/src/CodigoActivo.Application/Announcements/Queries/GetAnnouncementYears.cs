using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

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
public sealed class GetAnnouncementYearsQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor
) : IQueryHandler<GetAnnouncementYearsQuery, IReadOnlyList<int>>
{
    /// <summary>
    /// Handles the request to retrieve announcement years.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching int items.</returns>
    public Task<IReadOnlyList<int>> HandleAsync(
        GetAnnouncementYearsQuery query,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
            announcements
                .Query()
                .Select(a => a.CreatedAt.Year)
                .Distinct()
                .OrderByDescending(year => year),
            ct
        );
    }
}
