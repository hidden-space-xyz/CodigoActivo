using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Storage;
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
    [InlineData("{\"type\":\"doc\",\"type\":\"image\"}")]
    [InlineData("{\"content\":[{\"text\":\"a\",\"text\":\"b\"}]}")]
    public void IsValidJsonRepeatingAPropertyNameReturnsFalse(string value)
    {
        new JsonStringAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsEmptyRichTextRepeatingAPropertyNameReportsNoContent()
    {
        RichTextDocument
            .IsEmpty("{\"type\":\"doc\",\"type\":\"doc\",\"text\":\"hello\"}")
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("https://example.org/path?q=1")]
    [InlineData("http://localhost:8080")]
    public void IsValidHttpUrlAcceptsHttpAndHttps(string value)
    {
        new HttpUrlAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("//example.org/path")]
    [InlineData("https://user:password@example.org/path")]
    [InlineData("not a url")]
    public void IsValidHttpUrlRejectsUnsafeOrAmbiguousUrls(string value)
    {
        new HttpUrlAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidHttpUrlAllowsNullOptionalValue()
    {
        new HttpUrlAttribute().IsValid(null).Should().BeTrue();
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

    [Fact]
    public void GetValidationResultNotDefaultOrFutureDateNullableWithoutValueSucceeds()
    {
        DateOnly? unset = null;

        Validate(unset).Should().BeNull();
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
    public void IsValidSpanishNationalIdAcceptsDniAndNieWithTheirControlLetter(string value)
    {
        new SpanishNationalIdAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
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
    [InlineData(" - ")]
    [InlineData(" - - ")]
    public void IsValidSpanishNationalIdRejectsWrongFormatOrControlLetter(string value)
    {
        new SpanishNationalIdAttribute().IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidSpanishNationalIdLeavesMissingValuesToRequired(string? value)
    {
        new SpanishNationalIdAttribute().IsValid(value).Should().BeTrue();
    }

    [Fact]
    public void IsValidSpanishNationalIdRejectsNonStringValues()
    {
        new SpanishNationalIdAttribute().IsValid(12345678).Should().BeFalse();
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
