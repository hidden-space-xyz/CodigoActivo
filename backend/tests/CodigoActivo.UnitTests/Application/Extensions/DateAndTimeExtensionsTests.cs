using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class DateAndTimeExtensionsTests
{
    [Theory]
    [InlineData(2006, 2, 27, false)] // turned 18 two days ago
    [InlineData(2006, 2, 28, false)] // turns 18 today (a birthday makes you an adult)
    [InlineData(2006, 3, 1, true)] // turns 18 tomorrow
    [InlineData(2007, 1, 1, true)] // 17 years old
    [InlineData(1990, 1, 1, false)] // comfortably an adult
    [InlineData(2020, 6, 15, true)] // comfortably a minor
    [InlineData(2024, 2, 29, true)] // born today
    [InlineData(2029, 1, 1, true)] // birth date in the future
    public void IsMinor_BirthDateRelativeToToday_ClassifiesAge(
        int year,
        int month,
        int day,
        bool expected
    )
    {
        var today = new DateOnly(2024, 2, 29);

        new DateOnly(year, month, day).IsMinor(today).Should().Be(expected);
    }

    // Someone born on 29 February has no birthday in a common year: they stay a minor through
    // 28 February and turn 18 on 1 March.
    [Theory]
    [InlineData(2026, 2, 28, true)]
    [InlineData(2026, 3, 1, false)]
    public void IsMinor_LeapDayBirthDateInCommonYear_TurnsAdultOnMarchFirst(
        int year,
        int month,
        int day,
        bool expected
    )
    {
        var birthDate = new DateOnly(2008, 2, 29);

        birthDate.IsMinor(new DateOnly(year, month, day)).Should().Be(expected);
    }
}
