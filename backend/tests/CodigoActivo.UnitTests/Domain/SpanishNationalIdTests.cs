using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class SpanishNationalIdTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" - - ", null)]
    [InlineData("12345678Z", "12345678Z")]
    [InlineData(" 12345678-z ", "12345678Z")]
    [InlineData("x 1234567 l", "X1234567L")]
    [InlineData("y-1234-567-x", "Y1234567X")]
    public void NormalizeUppercasesAndDropsSpacesAndHyphens(string? value, string? expected)
    {
        SpanishNationalId.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("12345678Z")]
    [InlineData("00000000T")]
    [InlineData("X1234567L")]
    [InlineData("Y1234567X")]
    [InlineData("Z1234567R")]
    [InlineData("12345678z")]
    [InlineData(" 12345678-Z ")]
    [InlineData("1234 5678 Z")]
    [InlineData("x-1234567-l")]
    public void IsValidDniOrNieWithItsControlLetterIsAccepted(string value)
    {
        SpanishNationalId.IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("12345678A")]
    [InlineData("X1234567A")]
    [InlineData("1234567Z")]
    [InlineData("123456789Z")]
    [InlineData("W1234567L")]
    [InlineData("X12345678L")]
    [InlineData("1234567AZ")]
    [InlineData("12345678")]
    [InlineData("ABCDEFGHI")]
    [InlineData("１２３４５６７８Z")]
    [InlineData("-")]
    [InlineData(" - - ")]
    public void IsValidWrongFormatOrControlLetterIsRejected(string? value)
    {
        SpanishNationalId.IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "00000000T")]
    [InlineData(12_345_678, "12345678Z")]
    [InlineData(99_999_999, "99999999R")]
    public void FromDniNumberPadsTheNumberAndAppendsItsControlLetter(int number, string expected)
    {
        var dni = SpanishNationalId.FromDniNumber(number);

        dni.Should().Be(expected);
        SpanishNationalId.IsValid(dni).Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000_000)]
    public void FromDniNumberOutsideEightDigitsThrows(int number)
    {
        var act = () => SpanishNationalId.FromDniNumber(number);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
