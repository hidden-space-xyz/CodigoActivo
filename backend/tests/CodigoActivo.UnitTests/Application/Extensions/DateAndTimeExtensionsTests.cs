using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class DateAndTimeExtensionsTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void IsMinor_ExactlyEighteenToday_IsFalse()
    {
        var birthDate = Today.AddYears(-18);

        birthDate.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsMinor_EighteenthBirthdayTomorrow_IsTrue()
    {
        var birthDate = Today.AddYears(-18).AddDays(1);

        birthDate.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void IsMinor_EighteenthBirthdayYesterday_IsFalse()
    {
        var birthDate = Today.AddYears(-18).AddDays(-1);

        birthDate.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsMinor_FutureBirthdate_IsTrue()
    {
        var birthDate = Today.AddYears(5);

        birthDate.IsMinor().Should().BeTrue();
    }
}
