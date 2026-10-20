namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Defines the schedule of the periodic erasure of the dependents who came of age and of the notices
/// that announce it to their guardians.
/// </summary>
public sealed class AdultDependentPurgeOptions
{
    /// <summary>
    /// Gets or sets the interval between two runs.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
