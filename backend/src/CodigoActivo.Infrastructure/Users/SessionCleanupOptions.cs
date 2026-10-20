namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Defines configuration values for the periodic removal of expired session rows.
/// </summary>
public sealed class SessionCleanupOptions
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
