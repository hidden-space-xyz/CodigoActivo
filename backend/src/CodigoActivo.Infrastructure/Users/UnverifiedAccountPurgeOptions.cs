namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Defines the schedule of the periodic erasure of the accounts nobody verified in time.
/// </summary>
public sealed class UnverifiedAccountPurgeOptions
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
