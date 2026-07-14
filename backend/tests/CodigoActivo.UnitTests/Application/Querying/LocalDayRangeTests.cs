using AwesomeAssertions;
using CodigoActivo.Application.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

public sealed class LocalDayRangeTests
{
    private static readonly TimeZoneInfo MidnightGapZone = TimeZoneInfo.CreateCustomTimeZone(
        "Test/MidnightGap",
        TimeSpan.FromHours(-4),
        "Midnight Gap",
        "Midnight Gap Standard",
        "Midnight Gap Daylight",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2000, 1, 1),
                new DateTime(9999, 12, 31),
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 0, 0, 0),
                    9,
                    6
                ),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 0, 0, 0),
                    4,
                    5
                )
            ),
        ]
    );

    [Fact]
    public void LowerUtc_RegularDay_ReturnsUtcMidnightOfZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/Fixed", TimeSpan.FromHours(2), "F", "F");

        var lower = LocalDayRange.LowerUtc(new DateOnly(2026, 7, 12), zone);

        lower.Should().Be(new DateTimeOffset(2026, 7, 11, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void LowerUtc_DstGapCoversMidnight_ReturnsGapStartInstantInsteadOfThrowing()
    {
        var lower = LocalDayRange.LowerUtc(new DateOnly(2026, 9, 6), MidnightGapZone);

        lower.Should().Be(new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UpperExclusiveUtc_RegularDay_ReturnsStartOfNextDay()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/Fixed", TimeSpan.FromHours(2), "F", "F");

        var upper = LocalDayRange.UpperExclusiveUtc(new DateOnly(2026, 7, 12), zone);

        upper.Should().Be(new DateTimeOffset(2026, 7, 12, 22, 0, 0, TimeSpan.Zero));
    }
}
