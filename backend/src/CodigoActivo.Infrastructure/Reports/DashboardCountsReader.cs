using CodigoActivo.Application.Reports;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Reports;

/// <summary>
/// Reads the totals shown on the dashboard in a single round trip.
/// </summary>
/// <param name="context">Read-side database context the counts run on.</param>
public sealed class DashboardCountsReader(CodigoActivoReadDbContext context)
    : IDashboardCountsReader
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
