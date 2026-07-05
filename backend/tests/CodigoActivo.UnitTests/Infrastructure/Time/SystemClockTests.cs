using CodigoActivo.Infrastructure.Time;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Time;

public sealed class SystemClockTests
{
    [Theory]
    [InlineData("+14", 14)]
    [InlineData("-11", -11)]
    public void Today_reflects_the_configured_timezone(string id, int offsetHours)
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(offsetHours), id, id);
        var sut = new SystemClock(tz);

        var expected = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime
        );

        sut.Today.Should().Be(expected);
    }

    [Fact]
    public void Today_differs_between_the_two_extreme_timezones_when_they_straddle_midnight()
    {
        // Not asserting a specific value (wall-clock dependent); this exercises the conversion for
        // both directions and proves Today is timezone-derived rather than a fixed UTC date.
        var plus14 = new SystemClock(
            TimeZoneInfo.CreateCustomTimeZone("+14", TimeSpan.FromHours(14), "+14", "+14")
        );
        var minus11 = new SystemClock(
            TimeZoneInfo.CreateCustomTimeZone("-11", TimeSpan.FromHours(-11), "-11", "-11")
        );

        // The two zones are 25 hours apart, so their local dates always differ — by 1 day for most of
        // the day, and by 2 during the ~1h window when -11 is still on the previous date. Asserting the
        // bound (not a fixed gap) keeps this deterministic regardless of the wall clock.
        (plus14.Today > minus11.Today).Should().BeTrue();
        (plus14.Today.DayNumber - minus11.Today.DayNumber).Should().BeInRange(1, 2);
    }
}
