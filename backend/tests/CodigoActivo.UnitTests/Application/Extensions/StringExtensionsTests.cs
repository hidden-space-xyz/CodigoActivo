using AwesomeAssertions;
using CodigoActivo.Application.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class StringExtensionsTests
{
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
