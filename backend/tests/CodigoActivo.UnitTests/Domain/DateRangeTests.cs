using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class DateRangeTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "Test/PlusTwo",
        TimeSpan.FromHours(2),
        "Plus Two",
        "Plus Two"
    );

    private static readonly DateRange July = DateRange.FromStored(
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 31)
    );

    public static TheoryData<DateTimeOffset, DateTimeOffset, bool> Spans =>
        new()
        {
            { At(2026, 6, 30, 22), At(2026, 6, 30, 23), true },
            { At(2026, 6, 30, 21), At(2026, 6, 30, 23), false },
            { At(2026, 7, 31, 20), At(2026, 7, 31, 21, 59), true },
            { At(2026, 7, 31, 20), At(2026, 7, 31, 22), false },
        };

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }

    [Fact]
    public void InstantsInZoneStartsAtTheFirstLocalMidnightAndEndsAtTheOneAfterTheLastDay()
    {
        var (lowerInclusive, upperExclusive) = July.InstantsIn(PlusTwo);

        lowerInclusive.Should().Be(At(2026, 6, 30, 22));
        upperExclusive.Should().Be(At(2026, 7, 31, 22));
    }

    [Theory]
    [MemberData(nameof(Spans))]
    public void CoversSpanInZoneTellsWhetherItLiesWithinTheLocalDays(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool covered
    )
    {
        July.Covers(startsAt, endsAt, PlusTwo).Should().Be(covered);
    }
}
