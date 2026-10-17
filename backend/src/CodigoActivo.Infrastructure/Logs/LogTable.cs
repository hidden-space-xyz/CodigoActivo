namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Describes one of the log tables: its name and how long its entries are kept. Both are
/// partitioned by UTC day of <c>occurred_at</c>, in partitions named <c>&lt;table&gt;_pYYYYMMDD</c>.
/// </summary>
/// <param name="Name">Name of the partitioned table.</param>
/// <param name="Retention">Age after which an entry is removed.</param>
public sealed record LogTable(string Name, TimeSpan Retention)
{
    /// <summary>
    /// Gets the table of technical events, kept for 30 days.
    /// </summary>
    public static LogTable Diagnostics { get; } =
        new("diagnostic_log_entries", TimeSpan.FromDays(30));

    /// <summary>
    /// Gets the table of audited commands, kept for 60 days.
    /// </summary>
    public static LogTable Audit { get; } = new("audit_log_entries", TimeSpan.FromDays(60));

    /// <summary>
    /// Gets every log table.
    /// </summary>
    public static IReadOnlyList<LogTable> All { get; } = [Diagnostics, Audit];
}
