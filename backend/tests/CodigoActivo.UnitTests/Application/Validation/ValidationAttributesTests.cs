using CodigoActivo.Application.Validation;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Validation;

public sealed class ValidationAttributesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- NotBlankAttribute -------------------------------------------------

    [Fact]
    public void NotBlank_is_valid_for_non_string_values()
    {
        // Only strings are constrained; anything else (incl. null) passes.
        new NotBlankAttribute().IsValid(123).Should().BeTrue();
        new NotBlankAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void NotBlank_is_invalid_for_blank_or_whitespace_strings(string value)
    {
        new NotBlankAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void NotBlank_is_valid_for_a_non_blank_string()
    {
        new NotBlankAttribute().IsValid("Acme").Should().BeTrue();
    }

    // ---- JsonStringAttribute ----------------------------------------------

    [Fact]
    public void JsonString_is_valid_for_non_string_values()
    {
        new JsonStringAttribute().IsValid(42).Should().BeTrue();
        new JsonStringAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("null")]
    public void JsonString_is_valid_for_well_formed_json(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("{\"a\":}")]
    [InlineData("")]
    public void JsonString_is_invalid_for_malformed_json(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeFalse();
    }

    // ---- NotDefaultOrFutureDateAttribute -----------------------------------

    [Fact]
    public void NotDefaultOrFutureDate_is_valid_for_non_dateonly_values()
    {
        new NotDefaultOrFutureDateAttribute().IsValid("2024-01-01").Should().BeTrue();
        new NotDefaultOrFutureDateAttribute().IsValid(null).Should().BeTrue();
    }

    [Fact]
    public void NotDefaultOrFutureDate_is_invalid_for_the_default_date()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(default(DateOnly)).Should().BeFalse();
    }

    [Fact]
    public void NotDefaultOrFutureDate_is_invalid_for_a_future_date()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(Today.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void NotDefaultOrFutureDate_is_valid_for_today()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(Today).Should().BeTrue();
    }

    [Fact]
    public void NotDefaultOrFutureDate_is_valid_for_a_past_date()
    {
        new NotDefaultOrFutureDateAttribute().IsValid(new DateOnly(2000, 1, 1)).Should().BeTrue();
    }
}
