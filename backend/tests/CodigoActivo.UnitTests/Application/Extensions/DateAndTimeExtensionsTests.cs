using CodigoActivo.Application.Extensions;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class DateAndTimeExtensionsTests
{
    // IsMinor reads the wall clock (DateTime.UtcNow), so anchor every case to "today".
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void IsMinor_is_false_for_someone_exactly_eighteen_today()
    {
        // Born exactly 18 years ago today -> age 18 -> adult.
        var birthDate = Today.AddYears(-18);

        birthDate.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsMinor_is_true_for_seventeen_year_old()
    {
        var birthDate = Today.AddYears(-17);

        birthDate.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void IsMinor_is_true_when_eighteenth_birthday_is_tomorrow()
    {
        // Turns 18 tomorrow: still 17 today (exercises the birthDate > today.AddYears(-age) decrement).
        var birthDate = Today.AddYears(-18).AddDays(1);

        birthDate.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void IsMinor_is_false_when_eighteenth_birthday_was_yesterday()
    {
        var birthDate = Today.AddYears(-18).AddDays(-1);

        birthDate.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsMinor_is_true_for_a_future_birthdate()
    {
        var birthDate = Today.AddYears(5);

        birthDate.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void IsMinor_is_false_for_a_clear_adult()
    {
        var birthDate = Today.AddYears(-40);

        birthDate.IsMinor().Should().BeFalse();
    }
}
