using AwesomeAssertions;
using CodigoActivo.Application.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

public sealed class TextSearchTests
{
    private sealed record Row(string? Name);

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  Hello  ", "hello")]
    [InlineData("HELLO", "hello")]
    [InlineData("áéíóú", "aeiou")]
    [InlineData("ÁÉÍÓÚ", "aeiou")]
    [InlineData("Fundación Ávila", "fundacion avila")]
    [InlineData("\tMíguez\n", "miguez")]
    public void Normalize_trims_lowercases_and_folds_acute_vowels(string input, string expected)
    {
        TextSearch.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Fundación Ávila", "avila", true)]
    [InlineData("BETA", "beta", true)]
    [InlineData("https://beta.org", "eta", true)]
    [InlineData("Hello World", "world", true)]
    [InlineData("Banco", "avila", false)]
    [InlineData("Alpha", "beta", false)]
    public void Contains_matches_folded_case_insensitive_substrings(
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
    public void Contains_folds_the_column_so_accented_source_matches_plain_term()
    {
        var rows = new List<Row> { new("Fundación Ávila"), new("Banco Popular") }.AsQueryable();
        var predicate = TextSearch.Contains<Row>(r => r.Name, TextSearch.Normalize("AVILA"));

        var matched = rows.Where(predicate).ToList();

        matched.Should().ContainSingle().Which.Name.Should().Be("Fundación Ávila");
    }

    [Fact]
    public void Contains_treats_a_null_selected_value_as_no_match()
    {
        var predicate = TextSearch.Contains<Row>(r => r.Name, TextSearch.Normalize("x")).Compile();

        predicate(new Row(null)).Should().BeFalse();
    }
}
