using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Validation;

public sealed class ValidationAttributesTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);

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

    [Theory]
    [InlineData(2026, 7, 5)]
    [InlineData(2027, 1, 1)]
    public void GetValidationResult_NotDefaultOrFutureDateFutureDate_Fails(
        int year,
        int month,
        int day
    )
    {
        var result = Validate(new DateOnly(year, month, day));

        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(2026, 7, 4)]
    [InlineData(2026, 7, 3)]
    [InlineData(2000, 1, 1)]
    public void GetValidationResult_NotDefaultOrFutureDateTodayOrPast_Succeeds(
        int year,
        int month,
        int day
    )
    {
        var result = Validate(new DateOnly(year, month, day));

        result.Should().BeNull();
    }

    [Fact]
    public void GetValidationResult_NotDefaultOrFutureDateDefaultDate_Fails()
    {
        var result = Validate(default(DateOnly));

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetValidationResult_NotDefaultOrFutureDateNonDateOnlyValue_Succeeds()
    {
        Validate("2024-01-01").Should().BeNull();
        Validate(null).Should().BeNull();
    }

    [Fact]
    public void GetValidationResult_NotDefaultOrFutureDateFutureDate_NamesTheOffendingMember()
    {
        var result = Validate(Today.AddDays(1));

        result!.MemberNames.Should().Equal(nameof(Holder.BirthDate));
    }

    private static ValidationResult? Validate(object? value)
    {
        using var services = new ServiceCollection()
            .AddSingleton<IClock>(new TestClock(today: Today))
            .BuildServiceProvider();

        var context = new ValidationContext(new Holder(), services, items: null)
        {
            MemberName = nameof(Holder.BirthDate),
        };

        return new NotDefaultOrFutureDateAttribute().GetValidationResult(value, context);
    }

    private sealed class Holder
    {
        public DateOnly BirthDate { get; set; }
    }
}
