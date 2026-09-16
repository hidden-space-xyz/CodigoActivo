using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Time;

/// <summary>
/// Provides UTC and local dates using the configured time zone.
/// </summary>
/// <param name="timeZone">Time zone used to calculate local dates.</param>
/// <param name="timeProvider">Provider used as the source of the current time.</param>
public sealed class SystemClock(TimeZoneInfo timeZone, TimeProvider? timeProvider = null) : IClock
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets the utc now value.
    /// </summary>
    public DateTimeOffset UtcNow => time.GetUtcNow();

    /// <summary>
    /// Gets the today value.
    /// </summary>
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), timeZone).DateTime);

    /// <summary>
    /// Gets the time zone value.
    /// </summary>
    public TimeZoneInfo TimeZone => timeZone;
}
