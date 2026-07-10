using AwesomeAssertions;
using CodigoActivo.Infrastructure.Time;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Time;

public sealed class SystemClockTests
{
    [Theory]
    [InlineData("+14", 14)]
    [InlineData("-11", -11)]
    public void Today_ConfiguredTimezone_ReflectsOffset(string id, int offsetHours)
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(offsetHours), id, id);
        var sut = new SystemClock(tz);

        var expected = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime
        );

        sut.Today.Should().Be(expected);
    }
}
