namespace CodigoActivo.Application.Abstractions.Time;

/// <summary>
/// Provides the current instant and the local dates of the time zone the application is configured
/// with.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current instant, with a UTC offset of zero.
    /// </summary>
    public DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current calendar date in <see cref="TimeZone"/>, which can differ from the UTC date
    /// around midnight.
    /// </summary>
    public DateOnly Today { get; }

    /// <summary>
    /// Gets the time zone local days are read in, such as the days of the date filters: the IANA zone
    /// set by <c>APP_TIMEZONE</c>, or the zone of the host when it is unset.
    /// </summary>
    public TimeZoneInfo TimeZone { get; }
}
