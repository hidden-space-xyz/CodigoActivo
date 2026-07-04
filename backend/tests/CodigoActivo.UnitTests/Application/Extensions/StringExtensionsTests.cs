using CodigoActivo.Application.Extensions;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class StringExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void NormalizeOrNull_returns_null_for_null_empty_or_whitespace(string? value)
    {
        value.NormalizeOrNull().Should().BeNull();
    }

    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("  Acme  ", "Acme")]
    [InlineData("\tAcme\n", "Acme")]
    [InlineData("a b", "a b")]
    public void NormalizeOrNull_trims_and_returns_value_when_meaningful(string value, string expected)
    {
        value.NormalizeOrNull().Should().Be(expected);
    }
}
