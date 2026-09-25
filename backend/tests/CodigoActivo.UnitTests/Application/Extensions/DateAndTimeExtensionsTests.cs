using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class DateAndTimeExtensionsTests
{
    [Theory]
    [InlineData(2006, 2, 27, false)]
    [InlineData(2006, 2, 28, false)]
    [InlineData(2006, 3, 1, true)]
    [InlineData(2007, 1, 1, true)]
    [InlineData(1990, 1, 1, false)]
    [InlineData(2020, 6, 15, true)]
    [InlineData(2024, 2, 29, true)]
    [InlineData(2029, 1, 1, true)]
    public void IsMinorBirthDateRelativeToTodayClassifiesAge(
        int year,
        int month,
        int day,
        bool expected
    )
    {
        var today = new DateOnly(2024, 2, 29);

        new DateOnly(year, month, day).IsMinor(today).Should().Be(expected);
    }

    [Theory]
    [InlineData(2015, 5, 5, 2026, 5, 4, 10)]
    [InlineData(2015, 5, 5, 2026, 5, 5, 11)]
    [InlineData(2015, 5, 5, 2026, 12, 31, 11)]
    [InlineData(2008, 2, 29, 2026, 2, 28, 17)]
    [InlineData(2008, 2, 29, 2026, 3, 1, 18)]
    [InlineData(2026, 7, 4, 2026, 7, 4, 0)]
    public void AgeOnBirthDateCountsCompletedYears(
        int birthYear,
        int birthMonth,
        int birthDay,
        int year,
        int month,
        int day,
        int expected
    )
    {
        var birthDate = new DateOnly(birthYear, birthMonth, birthDay);

        birthDate.AgeOn(new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 2, 28, true)]
    [InlineData(2026, 3, 1, false)]
    public void IsMinorLeapDayBirthDateInCommonYearTurnsAdultOnMarchFirst(
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
