using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class DateAndTimeExtensionsTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void IsMinor_is_false_for_someone_exactly_eighteen_today()
    {
        var birthDate = Today.AddYears(-18);

        birthDate.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsMinor_is_true_when_eighteenth_birthday_is_tomorrow()
    {
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
}
