using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Validation;

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

    private const string FileUrl = "/api/files/0f8fad5b-d9cb-469f-a165-70867728950e/content";

    private static string Doc(string content)
    {
        return "{\"type\":\"doc\",\"content\":[" + content + "]}";
    }

    private static string Paragraph(string marks)
    {
        return "{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Hola\",\"marks\":["
            + marks
            + "]}]}";
    }

    [Theory]
    [InlineData(42)]
    [InlineData(null)]
    public void IsValidRichTextNonStringValuesReturnsTrue(object? value)
    {
        new RichTextAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"type\":\"doc\"}")]
    [InlineData("{\"type\":\"doc\",\"content\":[]}")]
    public void IsValidRichTextEmptyDocumentsReturnsTrue(string value)
    {
        new RichTextAttribute().IsValid(value).Should().BeTrue();
    }

    [Fact]
    public void IsValidRichTextEditorOutputReturnsTrue()
    {
        var value = Doc(
            "{\"type\":\"heading\",\"attrs\":{\"level\":2,\"textAlign\":\"center\"},\"content\":[{\"type\":\"text\",\"text\":\"Título\"}]},"
                + "{\"type\":\"paragraph\",\"attrs\":{\"textAlign\":null},\"content\":[{\"type\":\"text\",\"text\":\"Enlace\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"https://example.org/a\",\"target\":\"_blank\",\"rel\":\"noopener nofollow\",\"class\":null,\"title\":null}},{\"type\":\"bold\"}]},{\"type\":\"hardBreak\"},{\"type\":\"text\",\"text\":\"Color\",\"marks\":[{\"type\":\"textStyle\",\"attrs\":{\"color\":\"#ff0000\"}},{\"type\":\"highlight\",\"attrs\":{\"color\":\"rgb(255, 255, 0)\"}}]}]},"
                + "{\"type\":\"image\",\"attrs\":{\"src\":\""
                + FileUrl
                + "\",\"alt\":\"foto.png\",\"title\":null,\"width\":null,\"height\":null,\"textAlign\":null}},"
                + "{\"type\":\"orderedList\",\"attrs\":{\"start\":1,\"type\":null},\"content\":[{\"type\":\"listItem\",\"content\":[{\"type\":\"paragraph\"}]}]},"
                + "{\"type\":\"table\",\"content\":[{\"type\":\"tableRow\",\"content\":[{\"type\":\"tableHeader\",\"attrs\":{\"colspan\":1,\"rowspan\":1,\"colwidth\":[120]},\"content\":[{\"type\":\"paragraph\"}]}]}]},"
                + "{\"type\":\"codeBlock\",\"attrs\":{\"language\":null},\"content\":[{\"type\":\"text\",\"text\":\"x\"}]},"
                + "{\"type\":\"blockquote\",\"content\":[{\"type\":\"paragraph\"}]},{\"type\":\"horizontalRule\"}"
        );

        new RichTextAttribute().IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"mailto:info@example.org\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"tel:+34600000000\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"/events\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"#seccion\"}}")]
    [InlineData("{\"type\":\"textStyle\",\"attrs\":{\"color\":\"#abc\"}}")]
    public void IsValidRichTextSafeMarksReturnsTrue(string mark)
    {
        new RichTextAttribute().IsValid(Doc(Paragraph(mark))).Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"javascript:alert(1)\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\" JavaScript:alert(1)\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"data:text/html,x\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"//evil.example\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"/\\\\evil.example\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"https://user:pass@evil.example\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"\"}}")]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":null}}")]
    [InlineData("{\"type\":\"link\"}")]
    [InlineData(
        "{\"type\":\"link\",\"attrs\":{\"href\":\"https://a.example\",\"target\":\"_self\"}}"
    )]
    [InlineData("{\"type\":\"link\",\"attrs\":{\"href\":\"https://a.example\",\"onclick\":\"x\"}}")]
    [InlineData("{\"type\":\"textStyle\",\"attrs\":{\"color\":\"red;background:url(x)\"}}")]
    [InlineData("{\"type\":\"highlight\",\"attrs\":{\"color\":\"expression(alert(1))\"}}")]
    [InlineData("{\"type\":\"script\"}")]
    [InlineData("{\"type\":\"bold\",\"extra\":true}")]
    [InlineData("\"bold\"")]
    public void IsValidRichTextUnsafeMarksReturnsFalse(string mark)
    {
        new RichTextAttribute().IsValid(Doc(Paragraph(mark))).Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"type\":\"image\",\"attrs\":{\"src\":\"https://evil.example/x.png\"}}")]
    [InlineData("{\"type\":\"image\",\"attrs\":{\"src\":\"/api/files/not-a-guid/content\"}}")]
    [InlineData("{\"type\":\"image\",\"attrs\":{\"src\":null}}")]
    [InlineData("{\"type\":\"image\"}")]
    [InlineData("{\"type\":\"iframe\",\"attrs\":{\"src\":\"https://evil.example\"}}")]
    [InlineData("{\"type\":\"paragraph\",\"attrs\":{\"style\":\"color:red\"}}")]
    [InlineData("{\"type\":\"paragraph\",\"attrs\":{\"textAlign\":\"middle\"}}")]
    [InlineData("{\"type\":\"heading\",\"attrs\":{\"level\":7}}")]
    [InlineData("{\"type\":\"heading\",\"attrs\":{\"level\":\"2\"}}")]
    [InlineData("{\"type\":\"tableCell\",\"attrs\":{\"colspan\":0}}")]
    [InlineData("{\"type\":\"tableCell\",\"attrs\":{\"colwidth\":[\"100px\"]}}")]
    [InlineData("{\"type\":\"codeBlock\",\"attrs\":{\"language\":\"<script>\"}}")]
    [InlineData("{\"type\":\"paragraph\",\"html\":\"<b>x</b>\"}")]
    [InlineData("{\"type\":\"paragraph\",\"text\":\"x\"}")]
    [InlineData("{\"type\":\"text\"}")]
    [InlineData("{\"type\":\"text\",\"text\":1}")]
    [InlineData("{\"type\":\"text\",\"text\":\"x\",\"content\":[]}")]
    [InlineData("{\"type\":\"paragraph\",\"content\":{}}")]
    [InlineData("{\"type\":\"paragraph\",\"marks\":{}}")]
    [InlineData("{\"type\":\"paragraph\",\"attrs\":[]}")]
    [InlineData("{\"type\":1}")]
    [InlineData("[]")]
    public void IsValidRichTextUnsafeNodesReturnsFalse(string node)
    {
        new RichTextAttribute().IsValid(Doc(node)).Should().BeFalse();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("\"just a string\"")]
    [InlineData("{\"type\":\"paragraph\"}")]
    [InlineData("{\"type\":\"doc\",\"type\":\"doc\"}")]
    [InlineData("{\"content\":[]}")]
    public void IsValidRichTextMalformedOrForeignJsonReturnsFalse(string value)
    {
        new RichTextAttribute().IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValidRichTextWithoutImagesRefusesImageNodes()
    {
        var value = Doc("{\"type\":\"image\",\"attrs\":{\"src\":\"" + FileUrl + "\"}}");

        new RichTextAttribute().IsValid(value).Should().BeTrue();
        new RichTextAttribute { AllowImages = false }
            .IsValid(value)
            .Should()
            .BeFalse();
        new RichTextAttribute { AllowImages = false }
            .IsValid(Doc(Paragraph("{\"type\":\"bold\"}")))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsValidRichTextBeyondMaxDepthReturnsFalse()
    {
        var nested = "{\"type\":\"paragraph\"}";
        for (var i = 0; i < RichTextAllowlist.MaxDepth; i++)
        {
            nested = "{\"type\":\"blockquote\",\"content\":[" + nested + "]}";
        }

        new RichTextAttribute().IsValid(Doc(nested)).Should().BeFalse();
    }

    [Fact]
    public void IsValidRichTextBeyondMaxNodesReturnsFalse()
    {
        var paragraphs = string.Join(
            ",",
            Enumerable.Repeat("{\"type\":\"paragraph\"}", RichTextAllowlist.MaxNodes)
        );

        new RichTextAttribute().IsValid(Doc(paragraphs)).Should().BeFalse();
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
    [InlineData(" x-1234567-l ", true)]
    [InlineData("12345678A", false)]
    [InlineData(" - ", false)]
    public void IsValidSpanishNationalIdAppliesTheDomainRuleToTypedValues(
        string value,
        bool expected
    )
    {
        new SpanishNationalIdAttribute().IsValid(value).Should().Be(expected);
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
