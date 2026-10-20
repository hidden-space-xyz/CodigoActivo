namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Defines the schedule of the periodic deletion of the uploads no content used in time.
/// </summary>
public sealed class UnusedFilePurgeOptions
{
    /// <summary>
    /// Gets or sets the interval between two purges.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
