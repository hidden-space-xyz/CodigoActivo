namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Defines configuration values for the periodic removal of expired session rows.
/// </summary>
public sealed class SessionCleanupOptions
{
    /// <summary>
    /// Stores the shared default interval value.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Stores the shared max interval value.
    /// </summary>
    public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// Stores the shared default startup delay value.
    /// </summary>
    public static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the interval between two purges.
    /// </summary>
    public TimeSpan Interval { get; set; } = DefaultInterval;

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = DefaultStartupDelay;
}
