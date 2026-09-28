using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class TextNormalizationTests
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
    [InlineData("ana@test.com", "ana@test.com")]
    [InlineData("  Ana.Ruiz@Example.ORG ", "ana.ruiz@example.org")]
    public void NormalizeEmailOrNullTrimsAndLowercases(string? value, string? expected)
    {
        value.NormalizeEmailOrNull().Should().Be(expected);
    }
}
