using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Time;

public sealed class SystemClock(TimeZoneInfo timeZone, TimeProvider? timeProvider = null) : IClock
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), timeZone).DateTime);

    public TimeZoneInfo TimeZone => timeZone;
}
