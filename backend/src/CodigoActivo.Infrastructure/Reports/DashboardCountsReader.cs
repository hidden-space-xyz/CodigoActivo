using CodigoActivo.Application.Reports;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Reports;

/// <summary>
/// Counts the dashboard sources with plain SQL, each in a single round trip.
/// </summary>
/// <param name="context">Read-side database context the counts run on.</param>
public sealed class DashboardCountsReader(CodigoActivoReadDbContext context)
    : IDashboardCountsReader
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<DashboardCreatedCounts> CountCreatedAsync(
        IReadOnlyList<DateTimeOffset> thresholds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (thresholds.Zip(thresholds.Skip(1)).Any(pair => pair.First >= pair.Second))
        {
            throw new ArgumentException(
                "The thresholds must be in strictly ascending order.",
                nameof(thresholds)
            );
        }

        var bounds = thresholds.Select(threshold => threshold.ToUniversalTime()).ToArray();
        FormattableString sql = $"""
            WITH created (source, key, created_at) AS (
                SELECT 'users', user_type_id, created_at FROM users
                UNION ALL SELECT 'inscriptions', assignment_status_id, created_at FROM assignments
                UNION ALL SELECT 'events', NULL::uuid, created_at FROM events
                UNION ALL SELECT 'activities', NULL::uuid, created_at FROM activities
                UNION ALL SELECT 'resources', NULL::uuid, created_at FROM resources
                UNION ALL SELECT 'news', NULL::uuid, created_at FROM news
                UNION ALL SELECT 'partners', NULL::uuid, created_at FROM partners
            )
            SELECT source, key, width_bucket(created_at, {bounds}::timestamptz[]) AS "interval",
                count(*)::int AS count
            FROM created
            GROUP BY source, key, "interval"
            """;
        var rows = await context.Database.SqlQuery<CreatedRow>(sql).ToListAsync(ct);
        var bySource = rows.ToLookup(row => row.Source, StringComparer.Ordinal);

        return new DashboardCreatedCounts(
            Keyed(bySource["users"]),
            Keyed(bySource["inscriptions"]),
            Plain(bySource["events"]),
            Plain(bySource["activities"]),
            Plain(bySource["resources"]),
            Plain(bySource["news"]),
            Plain(bySource["partners"])
        );
    }

    private static List<KeyedIntervalCount> Keyed(IEnumerable<CreatedRow> rows)
    {
        return
        [
            .. rows.Select(row => new KeyedIntervalCount(row.Key!.Value, row.Interval, row.Count)),
        ];
    }

    private static List<IntervalCount> Plain(IEnumerable<CreatedRow> rows)
    {
        return [.. rows.Select(row => new IntervalCount(row.Interval, row.Count))];
    }

    internal sealed record CreatedRow
    {
        public string Source { get; init; } = string.Empty;

        public Guid? Key { get; init; }

        public int Interval { get; init; }

        public int Count { get; init; }
    }
}
