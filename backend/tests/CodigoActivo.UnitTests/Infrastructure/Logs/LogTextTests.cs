using AwesomeAssertions;
using CodigoActivo.Infrastructure.Logs;
using Serilog.Events;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Logs;

public sealed class LogTextTests
{
    [Fact]
    public void CleanLeavesStorableTextUntouched()
    {
        const string Text = "Un texto con acentos y emoji \U0001F600";

        LogText.Clean(Text, 100).Should().BeSameAs(Text);
    }

    [Fact]
    public void CleanDropsNulCharacters()
    {
        LogText.Clean("a\0b\0", 100).Should().Be("ab");
    }

    [Fact]
    public void CleanReplacesUnpairedSurrogates()
    {
        LogText.Clean("a\uD800b\uDC00", 100).Should().Be("a�b�");
        LogText.Clean("ab\uD800", 100).Should().Be("ab�");
    }

    [Fact]
    public void CleanKeepsSurrogatePairsOfATextItCleans()
    {
        LogText.Clean("a\0\U0001F600b", 100).Should().Be("a\U0001F600b");
    }

    [Fact]
    public void CleanTruncatesWithoutSplittingASurrogatePair()
    {
        LogText.Clean("ab\U0001F600", 3).Should().Be("ab");
        LogText.Clean("ab\U0001F600", 4).Should().Be("ab\U0001F600");
        LogText.Clean("abcdef", 4).Should().Be("abcd");
    }

    [Fact]
    public void FromPropertiesWritesNothingWithoutValues()
    {
        LogJson.FromProperties([]).Should().BeNull();
    }

    [Fact]
    public void FromPropertiesWritesNumbersJsonCannotHoldAsText()
    {
        LogJson
            .FromProperties([
                new LogEventProperty("Ratio", new ScalarValue(double.NaN)),
                new LogEventProperty("Limit", new ScalarValue(double.PositiveInfinity)),
                new LogEventProperty("Count", new ScalarValue(3)),
                new LogEventProperty("Share", new ScalarValue(0.5)),
            ])
            .Should()
            .Be("""{"Ratio":"NaN","Limit":"Infinity","Count":3,"Share":0.5}""");
    }

    [Fact]
    public void FromPropertiesCleansTextsAndWritesNestedValues()
    {
        LogJson
            .FromProperties([
                new LogEventProperty("Text", new ScalarValue("a\0b")),
                new LogEventProperty("Flag", new ScalarValue(true)),
                new LogEventProperty("Missing", new ScalarValue(null)),
                new LogEventProperty(
                    "Items",
                    new SequenceValue([new ScalarValue(1), new ScalarValue("x")])
                ),
                new LogEventProperty(
                    "Item",
                    new StructureValue([new LogEventProperty("Kind", new ScalarValue("Event"))])
                ),
            ])
            .Should()
            .Be(
                """{"Text":"ab","Flag":true,"Missing":null,"Items":[1,"x"],"Item":{"Kind":"Event"}}"""
            );
    }

    [Fact]
    public void FromPropertiesWritesEveryKindOfScalarItsOwnWay()
    {
        LogJson
            .FromProperties([
                new LogEventProperty("Large", new ScalarValue(ulong.MaxValue)),
                new LogEventProperty("Amount", new ScalarValue(1.5m)),
                new LogEventProperty("Single", new ScalarValue(0.25f)),
                new LogEventProperty("Unbounded", new ScalarValue(float.NegativeInfinity)),
                new LogEventProperty(
                    "At",
                    new ScalarValue(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero))
                ),
                new LogEventProperty(
                    "Local",
                    new ScalarValue(new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc))
                ),
                new LogEventProperty(
                    "Id",
                    new ScalarValue(new Guid("0b1c2d3e-0000-4000-8000-000000000001"))
                ),
                new LogEventProperty("Nameless", new ScalarValue(new Nameless())),
            ])
            .Should()
            .Be(
                """{"Large":18446744073709551615,"Amount":1.5,"Single":0.25,"Unbounded":"-Infinity","At":"2026-07-04T12:00:00+00:00","Local":"2026-07-04T12:00:00Z","Id":"0b1c2d3e-0000-4000-8000-000000000001","Nameless":""}"""
            );
    }

    [Fact]
    public void FromPropertiesWritesDictionariesAsObjectsWithCleanedKeys()
    {
        LogJson
            .FromProperties([
                new LogEventProperty(
                    "Counts",
                    new DictionaryValue([
                        new(new ScalarValue(1), new ScalarValue("uno")),
                        new(new ScalarValue("b\0"), new ScalarValue(2)),
                    ])
                ),
            ])
            .Should()
            .Be("""{"Counts":{"1":"uno","b":2}}""");
    }

    [Fact]
    public void FromPropertiesWritesUnknownValuesAsTheirRenderedText()
    {
        LogJson
            .FromProperties([new LogEventProperty("Custom", new RenderedValue("a\0b"))])
            .Should()
            .Be("""{"Custom":"ab"}""");
    }

    private sealed class Nameless
    {
        public override string? ToString()
        {
            return null;
        }
    }

    private sealed class RenderedValue(string text) : LogEventPropertyValue
    {
        public override void Render(
            TextWriter output,
            string? format = null,
            IFormatProvider? formatProvider = null
        )
        {
            output.Write(text);
        }
    }
}
