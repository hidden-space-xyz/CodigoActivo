namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Defines the schedule of the periodic erasure of the dependents who came of age and of the notices
/// that announce it to their guardians.
/// </summary>
public sealed class AdultDependentPurgeOptions
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
    /// Gets or sets the interval between two runs.
    /// </summary>
    public TimeSpan Interval { get; set; } = DefaultInterval;

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = DefaultStartupDelay;
}
