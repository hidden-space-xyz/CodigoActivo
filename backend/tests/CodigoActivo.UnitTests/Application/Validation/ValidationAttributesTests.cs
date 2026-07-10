using AwesomeAssertions;
using CodigoActivo.Application.Validation;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Validation;

public sealed class ValidationAttributesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void IsValid_NotBlankNonStringValues_ReturnsTrue()
    {
        new NotBlankAttribute().IsValid(123).Should().BeTrue();
        new NotBlankAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsValid_NotBlankBlankOrWhitespaceString_ReturnsFalse(string value)
    {
        new NotBlankAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValid_NotBlankNonBlankString_ReturnsTrue()
    {
        new NotBlankAttribute().IsValid("Acme").Should().BeTrue();
    }

    [Fact]
    public void IsValid_JsonStringNonStringValues_ReturnsTrue()
    {
        new JsonStringAttribute().IsValid(42).Should().BeTrue();
        new JsonStringAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"just a string\"")]
    [InlineData("null")]
    public void IsValid_WellFormedJson_ReturnsTrue(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("{\"a\":}")]
    [InlineData("")]
    public void IsValid_MalformedJson_ReturnsFalse(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValid_NotDefaultOrFutureDateNonDateOnlyValues_ReturnsTrue()
    {
        new NotDefaultOrFutureDateAttribute().IsValid("2024-01-01").Should().BeTrue();
        new NotDefaultOrFutureDateAttribute().IsValid(null).Should().BeTrue();
    }

    [Fact]
    public void IsValid_DefaultDate_ReturnsFalse()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(default(DateOnly)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_FutureDate_ReturnsFalse()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(Today.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Today_ReturnsTrue()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(Today).Should().BeTrue();
    }

    [Fact]
    public void IsValid_PastDate_ReturnsTrue()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(new DateOnly(2000, 1, 1)).Should().BeTrue();
    }
}
