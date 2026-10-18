namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Describes one of the log tables. Both are partitioned by UTC day of <c>occurred_at</c>, in
/// partitions named <c>&lt;table&gt;_pYYYYMMDD</c>, and keep their entries for the retention
/// <see cref="LogRetentionOptions"/> sets.
/// </summary>
/// <param name="Name">Name of the partitioned table.</param>
public sealed record LogTable(string Name)
{
    /// <summary>
    /// Gets the table of technical events.
    /// </summary>
    public static LogTable Diagnostics { get; } = new("diagnostic_log_entries");

    /// <summary>
    /// Gets the table of audited commands.
    /// </summary>
    public static LogTable Audit { get; } = new("audit_log_entries");

    /// <summary>
    /// Gets every log table.
    /// </summary>
    public static IReadOnlyList<LogTable> All { get; } = [Diagnostics, Audit];
}
