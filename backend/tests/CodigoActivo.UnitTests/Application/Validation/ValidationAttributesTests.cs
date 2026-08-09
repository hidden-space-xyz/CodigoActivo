using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Validation;

public sealed class ValidationAttributesTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 7, 4);

    private readonly ServiceProvider services = new ServiceCollection()
        .AddSingleton<IClock>(new TestClock(today: Today))
        .BuildServiceProvider();

    public void Dispose()
    {
        services.Dispose();
    }

    [Fact]
    public void IsValidNotBlankNonStringValuesReturnsTrue()
    {
        new NotBlankAttribute().IsValid(123).Should().BeTrue();
        new NotBlankAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsValidNotBlankBlankOrWhitespaceStringReturnsFalse(string value)
    {
        new NotBlankAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidNotBlankNonBlankStringReturnsTrue()
    {
        new NotBlankAttribute().IsValid("Acme").Should().BeTrue();
    }

    [Fact]
    public void IsValidJsonStringNonStringValuesReturnsTrue()
    {
        new JsonStringAttribute().IsValid(42).Should().BeTrue();
        new JsonStringAttribute().IsValid(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"just a string\"")]
    [InlineData("null")]
    public void IsValidWellFormedJsonReturnsTrue(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("{\"a\":}")]
    [InlineData("")]
    public void IsValidMalformedJsonReturnsFalse(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData(2026, 7, 5)]
    [InlineData(2027, 1, 1)]
    public void GetValidationResultNotDefaultOrFutureDateFutureDateFails(
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
    public void GetValidationResultNotDefaultOrFutureDateTodayOrPastSucceeds(
        int year,
        int month,
        int day
    )
    {
        var result = Validate(new DateOnly(year, month, day));

        result.Should().BeNull();
    }

    [Fact]
    public void GetValidationResultNotDefaultOrFutureDateDefaultDateFails()
    {
        var result = Validate(default(DateOnly));

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetValidationResultNotDefaultOrFutureDateNonDateOnlyValueSucceeds()
    {
        Validate("2024-01-01").Should().BeNull();
        Validate(null).Should().BeNull();
    }

    [Fact]
    public void GetValidationResultNotDefaultOrFutureDateFutureDateNamesTheOffendingMember()
    {
        var result = Validate(Today.AddDays(1));

        result!.MemberNames.Should().Equal(nameof(Holder.BirthDate));
    }

    private ValidationResult? Validate(object? value)
    {
        var context = new ValidationContext(new Holder { BirthDate = Today }, services, items: null)
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
