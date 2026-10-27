namespace CodigoActivo.Infrastructure.Legal;

/// <summary>
/// Defines the schedule of the notices to the association's legal contact.
/// </summary>
public sealed class LegalNoticeOptions
{
    /// <summary>
    /// Gets or sets the interval between two runs, the longest a withdrawal or a consultation waits
    /// before its notice is queued.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the delay applied before the first run, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
