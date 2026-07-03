using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Time;

/// <summary>System <see cref="IClock"/> whose <see cref="Today"/> is evaluated in a fixed timezone.</summary>
public sealed class SystemClock(TimeZoneInfo timeZone) : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
}
