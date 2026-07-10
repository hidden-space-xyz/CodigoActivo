using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Time;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Time;

public sealed class SystemClockTests
{
    private static readonly DateTimeOffset Instant = new(2026, 7, 4, 23, 30, 0, TimeSpan.Zero);

    private static TimeZoneInfo Zone(int offsetHours)
    {
        var id = offsetHours.ToString(CultureInfo.InvariantCulture);
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(offsetHours), id, id);
    }

    [Fact]
    public void UtcNow_AnyTimezone_ReturnsTheProvidedInstantUnshifted()
    {
        var sut = new SystemClock(Zone(5), new FixedTimeProvider(Instant));

        sut.UtcNow.Should().Be(Instant);
    }

    // 23:30 UTC has already rolled over to the next day in any zone ahead of UTC by an hour or
    // more, while zones at or behind UTC are still on the previous date.
    [Theory]
    [InlineData(1, 2026, 7, 5)]
    [InlineData(14, 2026, 7, 5)]
    [InlineData(0, 2026, 7, 4)]
    [InlineData(-11, 2026, 7, 4)]
    public void Today_ConfiguredTimezone_ShiftsTheCalendarDate(
        int offsetHours,
        int year,
        int month,
        int day
    )
    {
        var sut = new SystemClock(Zone(offsetHours), new FixedTimeProvider(Instant));

        sut.Today.Should().Be(new DateOnly(year, month, day));
    }

    [Fact]
    public void TimeZone_ConfiguredTimezone_IsExposedUnchanged()
    {
        var zone = Zone(3);

        new SystemClock(zone).TimeZone.Should().Be(zone);
    }
}
