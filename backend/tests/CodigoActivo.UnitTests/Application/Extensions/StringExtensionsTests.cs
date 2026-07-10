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
    public void NormalizeOrNull_NullEmptyOrWhitespace_ReturnsNull(string? value)
    {
        value.NormalizeOrNull().Should().BeNull();
    }

    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("  Acme  ", "Acme")]
    [InlineData("\tAcme\n", "Acme")]
    [InlineData("a b", "a b")]
    public void NormalizeOrNull_MeaningfulValue_TrimsAndReturnsValue(string value, string expected)
    {
        value.NormalizeOrNull().Should().Be(expected);
    }
}
