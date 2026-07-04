using CodigoActivo.Domain.Common;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>Deterministic <see cref="IClock"/>; both fields are settable so a test can pin "now".</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTimeOffset? utcNow = null, DateOnly? today = null)
    {
        UtcNow = utcNow ?? new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
        Today = today ?? DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    public DateTimeOffset UtcNow { get; set; }

    public DateOnly Today { get; set; }

    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}
