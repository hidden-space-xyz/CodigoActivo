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

    /// <inheritdoc />
    public DateTimeOffset UtcNow => time.GetUtcNow();

    /// <inheritdoc />
    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), timeZone).DateTime);

    /// <inheritdoc />
    public TimeZoneInfo TimeZone => timeZone;
}
