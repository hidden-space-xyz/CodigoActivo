using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class DashboardRepository(CodigoActivoDbContext context) : IDashboardRepository
{
    public async Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default)
    {
        FormattableString sql = $"""
            SELECT
                (SELECT count(*)::int FROM events) AS events,
                (SELECT count(*)::int FROM activities) AS activities,
                (SELECT count(*)::int FROM resources) AS resources,
                (SELECT count(*)::int FROM announcements) AS announcements,
                (SELECT count(*)::int FROM partners) AS partners,
                (SELECT count(*)::int FROM users) AS users
            """;
        return await context.Database.SqlQuery<DashboardCounts>(sql).SingleAsync(ct);
    }
}
