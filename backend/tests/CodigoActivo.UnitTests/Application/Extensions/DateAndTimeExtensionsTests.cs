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
