namespace CodigoActivo.Domain.Common;

/// <summary>
/// Provides UTC and local dates using the configured time zone.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the utc now value.
    /// </summary>
    public DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the today value.
    /// </summary>
    public DateOnly Today { get; }

    /// <summary>
    /// Gets the time zone value.
    /// </summary>
    public TimeZoneInfo TimeZone { get; }
}
