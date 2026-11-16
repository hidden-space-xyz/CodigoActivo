using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Common;

public sealed class RichTextTests
{
    [Fact]
    public void HasSameContentAsIgnoresSpacesAndTheOrderOfKeys()
    {
        var compact = RichText.From("{\"type\":\"doc\",\"content\":[{\"text\":\"Hola\"}]}");
        var stored = RichText.From("{\"content\": [{\"text\": \"Hola\"}], \"type\": \"doc\"}");

        compact.HasSameContentAs(stored).Should().BeTrue();
    }

    [Fact]
    public void HasSameContentAsTellsADifferentTextOrOrderOfItems()
    {
        var hola = RichText.From("{\"content\":[{\"text\":\"Hola\"},{\"text\":\"Adiós\"}]}");

        hola.HasSameContentAs(RichText.From("{\"content\":[{\"text\":\"Hola\"}]}"))
            .Should()
            .BeFalse();
        hola.HasSameContentAs(
                RichText.From("{\"content\":[{\"text\":\"Adiós\"},{\"text\":\"Hola\"}]}")
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void HasSameContentAsUnreadableTextComparesItLiterally()
    {
        var broken = RichText.From("{\"type\":\"doc\"");

        broken.HasSameContentAs(RichText.From("{\"type\":\"doc\"")).Should().BeTrue();
        broken.HasSameContentAs(RichText.From("{\"type\": \"doc\"")).Should().BeFalse();
    }

    [Theory]
    [InlineData(
        "{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"/a\"}}]}",
        false
    )]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\" \"}]}", true)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}", true)]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"Hola\"}]}", false)]
    [InlineData("not json", true)]
    [InlineData(" ", true)]
    public void IsEmptyTellsWhetherAnyTextOrImageRemains(string json, bool empty)
    {
        RichText.From(json).IsEmpty.Should().Be(empty);
    }
}
