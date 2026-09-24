using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves dashboard data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class DashboardRepository(CodigoActivoDbContext context) : IDashboardRepository
{
    /// <summary>
    /// Gets the requested counts.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard counts.</returns>
    public async Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default)
    {
        FormattableString sql = $"""
            SELECT
                (SELECT count(*)::int FROM events) AS events,
                (SELECT count(*)::int FROM activities) AS activities,
                (SELECT count(*)::int FROM resources) AS resources,
                (SELECT count(*)::int FROM news) AS news,
                (SELECT count(*)::int FROM partners) AS partners,
                (SELECT count(*)::int FROM users) AS users
            """;
        return await context.Database.SqlQuery<DashboardCounts>(sql).SingleAsync(ct);
    }
}
