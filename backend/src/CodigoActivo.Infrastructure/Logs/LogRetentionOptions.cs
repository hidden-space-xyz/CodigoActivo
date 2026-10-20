namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Defines how long the entries of each log table are kept.
/// </summary>
public sealed class LogRetentionOptions
{
    /// <summary>
    /// Stores the shared default retention of the diagnostic log.
    /// </summary>
    public static readonly TimeSpan DefaultDiagnostics = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the age after which a diagnostic entry is removed.
    /// </summary>
    public TimeSpan Diagnostics { get; set; } = DefaultDiagnostics;

    /// <summary>
    /// Gets or sets the age after which an audit entry is removed.
    /// </summary>
    public TimeSpan Audit { get; set; } = TimeSpan.FromDays(60);

    /// <summary>
    /// Gets the retention of a log table.
    /// </summary>
    /// <param name="table">Log table.</param>
    /// <returns>The age after which an entry of the table is removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The table is not one of <see cref="LogTable.All"/>.</exception>
    public TimeSpan Of(LogTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return table == LogTable.Diagnostics ? Diagnostics
            : table == LogTable.Audit ? Audit
            : throw new ArgumentOutOfRangeException(
                nameof(table),
                table.Name,
                "Unknown log table."
            );
    }
}
