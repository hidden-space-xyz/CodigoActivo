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
    public void FromValueWritesNothingForAMissingValue()
    {
        LogJson.FromValue(null).Should().BeNull();
        LogJson.FromValue(new ScalarValue(null)).Should().BeNull();
    }
}
