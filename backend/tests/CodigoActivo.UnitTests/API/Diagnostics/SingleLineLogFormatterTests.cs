using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.API.Diagnostics;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace CodigoActivo.UnitTests.API.Diagnostics;

public sealed class SingleLineLogFormatterTests
{
    private const string Category = "CodigoActivo.Application.Files.OrphanFileCleaner";
    private const string Mask = "<id>";
    private const string Lowercase = "3f6a0f4c-1c8a-4a3f-9a6e-2f0b7d5c8e11";
    private const string Uppercase = "A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D";
    private const string Braced = "{7c9e6679-7425-40de-944b-e07fc1f90ae7}";
    private const string Compact = "7c9e6679742540de944be07fc1f90ae7";
    private const string Digest =
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static readonly DateTimeOffset Moment = new(
        2026,
        9,
        20,
        10,
        30,
        15,
        123,
        TimeSpan.Zero
    );

    private static string Write(string template, Exception? exception = null)
    {
        var logEvent = new LogEvent(
            Moment,
            LogEventLevel.Warning,
            exception,
            new MessageTemplateParser().Parse(template),
            [new LogEventProperty("SourceContext", new ScalarValue(Category))]
        );

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        new SingleLineLogFormatter().Format(logEvent, output);

        return output.ToString();
    }

    private static UnauthorizedAccessException Denied(string path)
    {
        return new UnauthorizedAccessException(
            $"Access to the path '{path}' is denied.",
            new IOException($"The file '{path}' is in use by {Compact}")
        );
    }

    [Fact]
    public void FormatWithIdentifiersInTheMessageWritesTheMaskInsteadOfEachOne()
    {
        var line = Write($"Deleting {Lowercase} and {Uppercase} and {Braced} and user:{Compact}");

        line.Should()
            .NotContain(Lowercase)
            .And.NotContain(Uppercase)
            .And.NotContain(Braced.Trim('{', '}'))
            .And.NotContain(Compact);
        line.Should().Contain($"Deleting {Mask} and {Mask} and {{{Mask}}} and user:{Mask}");
    }

    [Fact]
    public void FormatWithIdentifiersInTheExceptionChainWritesTheMaskInsteadOfEachOne()
    {
        var line = Write("Orphan file cleanup failed", Denied($"/app/files/{Lowercase}.png"));

        line.Should()
            .NotContain(Lowercase)
            .And.NotContain(Compact)
            .And.Contain($"Access to the path '/app/files/{Mask}.png' is denied.")
            .And.Contain($"The file '/app/files/{Mask}.png' is in use by {Mask}")
            .And.Contain("UnauthorizedAccessException")
            .And.Contain("IOException");
    }

    [Fact]
    public void FormatWithAMaskedExceptionStillWritesOneSingleLine()
    {
        var line = Write(
            $"Deleting {Lowercase}",
            new InvalidOperationException($"first {Uppercase}\nsecond {Compact}\r\nthird")
        );

        line.Should().EndWith("\n");
        line.TrimEnd('\n').Should().NotContainAny("\n", "\r");
    }

    [Fact]
    public void FormatWithHexTextThatIsNotAnIdentifierLeavesItUntouched()
    {
        var text = $"SqlState 23505 hresult 0x80070005 hash a1b2c3d4 digest {Digest}";

        var line = Write(text);

        line.Should().EndWith($"{text}\n").And.NotContain(Mask);
    }

    [Fact]
    public void FormatWithAMaskedMessageKeepsTheTimestampLevelAndCategory()
    {
        var line = Write($"Deleting {Lowercase}");

        line.Should()
            .StartWith($"2026-09-20T10:30:15.123Z WRN {Category} Deleting {Mask}")
            .And.NotContain(Lowercase);
    }
}
