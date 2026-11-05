namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Defines the schedule of the periodic removal of the email changes and authenticator enrollments
/// that can no longer be confirmed.
/// </summary>
public sealed class ExpiredAccountChangeCleanupOptions
{
    /// <summary>
    /// Gets or sets the interval between two cleanups.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
