using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

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
                new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    9,
                    6
                ),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                    4,
                    5
                )
            ),
        ]
    );

    private static readonly TimeZoneInfo MidnightTwiceZone = TimeZoneInfo.CreateCustomTimeZone(
        "Test/MidnightTwice",
        TimeSpan.FromHours(-5),
        "Midnight Twice",
        "Midnight Twice Standard",
        "Midnight Twice Daylight",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified),
                    3,
                    8
                ),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                    new DateTime(1, 1, 1, 1, 0, 0, DateTimeKind.Unspecified),
                    11,
                    1
                )
            ),
        ]
    );

    [Fact]
    public void LowerUtcDstOverlapRepeatsMidnightReturnsTheFirstMidnight()
    {
        var lower = LocalDayRange.LowerUtc(new DateOnly(2026, 11, 1), MidnightTwiceZone);

        lower.Should().Be(new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void LowerUtcRegularDayReturnsUtcMidnightOfZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/Fixed", TimeSpan.FromHours(2), "F", "F");

        var lower = LocalDayRange.LowerUtc(new DateOnly(2026, 7, 12), zone);

        lower.Should().Be(new DateTimeOffset(2026, 7, 11, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void LowerUtcDstGapCoversMidnightReturnsGapStartInstantInsteadOfThrowing()
    {
        var lower = LocalDayRange.LowerUtc(new DateOnly(2026, 9, 6), MidnightGapZone);

        lower.Should().Be(new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UpperExclusiveUtcRegularDayReturnsStartOfNextDay()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/Fixed", TimeSpan.FromHours(2), "F", "F");

        var upper = LocalDayRange.UpperExclusiveUtc(new DateOnly(2026, 7, 12), zone);

        upper.Should().Be(new DateTimeOffset(2026, 7, 12, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UpperExclusiveUtcLastRepresentableDayReturnsMaxValueInsteadOfThrowing()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/Fixed", TimeSpan.FromHours(2), "F", "F");

        var upper = LocalDayRange.UpperExclusiveUtc(DateOnly.MaxValue, zone);

        upper.Should().Be(DateTimeOffset.MaxValue);
    }
}
