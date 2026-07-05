using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Time;

public sealed class SystemClock(TimeZoneInfo timeZone) : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);

    public TimeZoneInfo TimeZone => timeZone;
}
