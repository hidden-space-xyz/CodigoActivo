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
}
