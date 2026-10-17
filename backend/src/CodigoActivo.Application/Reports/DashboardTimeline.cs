using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Reports;

/// <summary>
/// The buckets of the dashboard window and the instants that separate them, so creation times can
/// be counted per bucket in the database. The intervals are, in order: before the previous period,
/// the previous period of the same length, one interval per bucket of the window, and after it.
/// </summary>
internal sealed class DashboardTimeline
{
    private const int PreviousInterval = 1;
    private const int FirstBucketInterval = 2;

    private DashboardTimeline(
        IReadOnlyList<DateOnly> buckets,
        IReadOnlyList<DateTimeOffset> thresholds
    )
    {
        Buckets = buckets;
        Thresholds = thresholds;
    }

    /// <summary>
    /// Gets the first day of every bucket of the window.
    /// </summary>
    public IReadOnlyList<DateOnly> Buckets { get; }

    /// <summary>
    /// Gets the instants that separate the intervals, in ascending order.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> Thresholds { get; }

    /// <summary>
    /// Builds the timeline of a window.
    /// </summary>
    /// <param name="start">First day of the window.</param>
    /// <param name="end">Last day of the window.</param>
    /// <param name="granularity">Bucket size: day, week or month.</param>
    /// <param name="zone">Time zone the days belong to.</param>
    /// <returns>The timeline.</returns>
    public static DashboardTimeline Create(
        DateOnly start,
        DateOnly end,
        string granularity,
        TimeZoneInfo zone
    )
    {
        var buckets = BucketsBetween(start, end, granularity);
        var previousStart = start.AddDays(-(end.DayNumber - start.DayNumber + 1));
        List<DateTimeOffset> thresholds =
        [
            LocalDayRange.LowerUtc(previousStart, zone),
            LocalDayRange.LowerUtc(start, zone),
            .. buckets.Skip(1).Select(bucket => LocalDayRange.LowerUtc(bucket, zone)),
            LocalDayRange.UpperExclusiveUtc(end, zone),
        ];
        return new DashboardTimeline(buckets, thresholds);
    }

    /// <summary>
    /// Counts what was created in each bucket of the window.
    /// </summary>
    /// <param name="key">Key of the series.</param>
    /// <param name="counts">Rows created per interval.</param>
    /// <returns>The series.</returns>
    public DashboardSeriesResponse Flow(string key, IEnumerable<IntervalCount> counts)
    {
        return new DashboardSeriesResponse(key, PerBucket(counts));
    }

    /// <summary>
    /// Summarizes the rows of a source: all of them, those of the window and those of the period
    /// before it.
    /// </summary>
    /// <param name="key">Key of the indicator.</param>
    /// <param name="counts">Rows created per interval.</param>
    /// <returns>The indicator.</returns>
    public DashboardKpiResponse Kpi(string key, IEnumerable<IntervalCount> counts)
    {
        var materialized = counts.ToList();
        return new DashboardKpiResponse(
            key,
            materialized.Sum(count => count.Count),
            PerBucket(materialized).Sum(),
            materialized
                .Where(count => count.Interval == PreviousInterval)
                .Sum(count => count.Count)
        );
    }

    private static List<DateOnly> BucketsBetween(DateOnly start, DateOnly end, string granularity)
    {
        var buckets = new List<DateOnly>();
        var cursor = BucketStart(start, granularity);
        while (cursor <= end)
        {
            buckets.Add(cursor);
            cursor = granularity switch
            {
                "day" => cursor.AddDays(1),
                "week" => cursor.AddDays(7),
                _ => cursor.AddMonths(1),
            };
        }

        return buckets;
    }

    private static DateOnly BucketStart(DateOnly date, string granularity)
    {
        return granularity switch
        {
            "day" => date,
            "week" => date.AddDays(-((date.DayOfWeek - DayOfWeek.Monday + 7) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1),
        };
    }

    private int[] PerBucket(IEnumerable<IntervalCount> counts)
    {
        var values = new int[Buckets.Count];
        foreach (var count in counts)
        {
            var bucket = count.Interval - FirstBucketInterval;
            if (bucket >= 0 && bucket < values.Length)
            {
                values[bucket] += count.Count;
            }
        }

        return values;
    }
}
