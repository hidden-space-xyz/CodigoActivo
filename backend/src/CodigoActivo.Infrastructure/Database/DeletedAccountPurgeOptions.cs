namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Defines the schedule of the periodic purge of deleted account copies whose retention ended.
/// </summary>
public sealed class DeletedAccountPurgeOptions
{
    /// <summary>
    /// Stores the shared default interval value.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

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
