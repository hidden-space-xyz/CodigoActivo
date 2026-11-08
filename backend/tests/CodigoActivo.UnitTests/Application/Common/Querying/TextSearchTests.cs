using AwesomeAssertions;
using CodigoActivo.Application.Common.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Querying;

public sealed class TextSearchTests
{
    private sealed record Row(string? Name);

    [Theory]
    [InlineData("  Hello  ", "hello")]
    [InlineData("áéíóú", "aeiou")]
    [InlineData("ÁÉÍÓÚ", "aeiou")]
    [InlineData("Fundación Ávila", "fundacion avila")]
    [InlineData("\tMíguez\n", "miguez")]
    [InlineData("àèìòù", "aeiou")]
    [InlineData("ÀÈÌÒÙ", "aeiou")]
    [InlineData("âêîôû", "aeiou")]
    [InlineData("ÂÊÎÔÛ", "aeiou")]
    [InlineData("äëïöü", "aeiou")]
    [InlineData("ÄËÏÖÜ", "aeiou")]
    [InlineData("Çç", "cc")]
    [InlineData("Pingüino Lluís Françoise Noël", "pinguino lluis francoise noel")]
    [InlineData("Ñandú España", "ñandu españa")]
    public void NormalizeMixedCaseWithAccentsAndWhitespaceTrimsLowercasesAndFoldsVowels(
        string input,
        string expected
    )
    {
        TextSearch.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Fundación Ávila", "avila", true)]
    [InlineData("Pingüino", "pinguino", true)]
    [InlineData("Pinguino", "pingüino", true)]
    [InlineData("FRANÇOISE", "francoise", true)]
    [InlineData("Noël à Paris", "NOEL A", true)]
    [InlineData("Peña", "pena", false)]
    [InlineData("BETA", "beta", true)]
    [InlineData("https://beta.org", "eta", true)]
    [InlineData("Hello World", "world", true)]
    [InlineData("Banco", "avila", false)]
    [InlineData("Alpha", "beta", false)]
    public void ContainsFoldedCaseInsensitiveSubstringReturnsExpectedMatch(
        string value,
        string rawTerm,
        bool expected
    )
    {
        var predicate = TextSearch
            .Contains<Row>(r => r.Name, TextSearch.Normalize(rawTerm))
            .Compile();

        predicate(new Row(value)).Should().Be(expected);
    }

    [Fact]
    public void ContainsNullSelectedValueReturnsFalse()
    {
        var predicate = TextSearch.Contains<Row>(r => r.Name, TextSearch.Normalize("x")).Compile();

        predicate(new Row(null)).Should().BeFalse();
    }
}
