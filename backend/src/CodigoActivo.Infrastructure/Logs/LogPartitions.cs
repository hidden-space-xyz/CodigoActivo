using System.Globalization;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Computes the daily partitions of the log tables: their names, their UTC bounds, the days kept
/// ready ahead of time and the instant before which entries are removed.
/// <para>
/// The cutoff is one hour later than the retention alone would give, and maintenance runs every
/// hour, so no entry is ever kept for longer than the retention of its table.
/// </para>
/// </summary>
public static class LogPartitions
{
    /// <summary>
    /// Gets how many days after today get their partition ahead of time.
    /// </summary>
    public const int DaysAhead = 2;

    private const string DayFormat = "yyyyMMdd";
    private const string PartitionInfix = "_p";

    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets the UTC day an instant falls in.
    /// </summary>
    /// <param name="instant">Instant to place.</param>
    /// <returns>The UTC day of the instant.</returns>
    public static DateOnly DayOf(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(instant.UtcDateTime);
    }

    /// <summary>
    /// Gets the first instant of a UTC day.
    /// </summary>
    /// <param name="day">UTC day.</param>
    /// <returns>Its midnight in UTC.</returns>
    public static DateTimeOffset StartOf(DateOnly day)
    {
        return new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the name of the partition of a table that holds one UTC day.
    /// </summary>
    /// <param name="table">Partitioned table.</param>
    /// <param name="day">UTC day.</param>
    /// <returns>The partition name, <c>&lt;table&gt;_pYYYYMMDD</c>.</returns>
    public static string NameOf(LogTable table, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(table);

        return $"{table.Name}{PartitionInfix}{day.ToString(DayFormat, CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Reads the day a partition of a table holds from its name.
    /// </summary>
    /// <param name="table">Partitioned table.</param>
    /// <param name="partition">Name of the partition.</param>
    /// <param name="day">Day the partition holds, when the name is one of ours.</param>
    /// <returns><see langword="true"/> when the name follows the daily pattern of the table.</returns>
    public static bool TryParseDay(LogTable table, string partition, out DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(partition);

        day = default;
        var prefix = table.Name + PartitionInfix;
        return partition.StartsWith(prefix, StringComparison.Ordinal)
            && DateOnly.TryParseExact(
                partition.AsSpan(prefix.Length),
                DayFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out day
            );
    }

    /// <summary>
    /// Gets the instant before which the entries of a table are removed.
    /// </summary>
    /// <param name="retention">Retention of the table.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The cutoff instant.</returns>
    public static DateTimeOffset CutoffOf(TimeSpan retention, DateTimeOffset now)
    {
        return now.ToUniversalTime() - retention + MaintenanceInterval;
    }

    /// <summary>
    /// Gets the days whose partitions are kept ready: today and the next <see cref="DaysAhead"/>.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns>The days in ascending order.</returns>
    public static IReadOnlyList<DateOnly> UpcomingDays(DateTimeOffset now)
    {
        var today = DayOf(now);
        return [.. Enumerable.Range(0, DaysAhead + 1).Select(today.AddDays)];
    }

    /// <summary>
    /// Tells whether a day may have a partition: it still holds entries younger than the cutoff
    /// and it is not further ahead than the days kept ready.
    /// </summary>
    /// <param name="retention">Retention of the table.</param>
    /// <param name="day">UTC day.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the day is inside the window of the table.</returns>
    public static bool IsWithinWindow(TimeSpan retention, DateOnly day, DateTimeOffset now)
    {
        return !IsExpired(day, CutoffOf(retention, now)) && day <= DayOf(now).AddDays(DaysAhead);
    }

    /// <summary>
    /// Tells whether a whole day lies before a cutoff, so its partition can be dropped.
    /// </summary>
    /// <param name="day">UTC day.</param>
    /// <param name="cutoff">Instant before which entries are removed.</param>
    /// <returns><see langword="true"/> when the day ends at or before the cutoff.</returns>
    public static bool IsExpired(DateOnly day, DateTimeOffset cutoff)
    {
        return StartOf(day.AddDays(1)) <= cutoff;
    }
}
