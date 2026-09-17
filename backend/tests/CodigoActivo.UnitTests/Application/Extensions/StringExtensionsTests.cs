using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class StringExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void NormalizeOrNullNullEmptyOrWhitespaceReturnsNull(string? value)
    {
        value.NormalizeOrNull().Should().BeNull();
    }

    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("  Acme  ", "Acme")]
    [InlineData("\tAcme\n", "Acme")]
    [InlineData("a b", "a b")]
    public void NormalizeOrNullMeaningfulValueTrimsAndReturnsValue(string value, string expected)
    {
        value.NormalizeOrNull().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("ana.ruiz@example.org", "a***@example.org")]
    [InlineData("a@example.org", "a***@example.org")]
    [InlineData("no-at-sign", "***")]
    [InlineData("@example.org", "***")]
    public void MaskEmailHidesTheLocalPartExceptItsFirstCharacter(string? value, string? expected)
    {
        value.MaskEmail().Should().Be(expected);
    }
}
