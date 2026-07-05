using CodigoActivo.Domain.Common;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    public DateOnly Today { get; set; } = new(2026, 7, 4);

    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}
