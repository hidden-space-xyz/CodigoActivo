using CodigoActivo.Application.Abstractions.Time;

namespace CodigoActivo.Infrastructure.Time;

/// <summary>
/// Provides UTC and local dates using the configured time zone.
/// </summary>
/// <param name="timeZone">Time zone used to calculate local dates.</param>
/// <param name="timeProvider">
/// Provider used as the source of the current time; <see langword="null"/> uses
/// <see cref="TimeProvider.System"/>.
/// </param>
public sealed class SystemClock(TimeZoneInfo timeZone, TimeProvider? timeProvider = null) : IClock
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets the current instant in UTC.
    /// </summary>
    public DateTimeOffset UtcNow => time.GetUtcNow();

    /// <summary>
    /// Gets the current calendar date in the configured time zone.
    /// </summary>
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), timeZone).DateTime);

    /// <summary>
    /// Gets the time zone used to calculate local dates.
    /// </summary>
    public TimeZoneInfo TimeZone => timeZone;
}
