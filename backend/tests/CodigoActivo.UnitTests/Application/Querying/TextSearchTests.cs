using CodigoActivo.Application.Querying;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

/// <summary>
/// Unit tests for <see cref="TextSearch"/>. The <see cref="TextSearch.Contains{T}"/> predicate is
/// compiled and executed in memory so the LOWER + REPLACE fold is exercised exactly as EF would
/// translate it.
/// </summary>
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
    public void Contains_matches_folded_case_insensitive_substrings(string value, string rawTerm, bool expected)
    {
        var predicate = TextSearch.Contains<Row>(r => r.Name, TextSearch.Normalize(rawTerm)).Compile();

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
    public void Contains_throws_when_the_selected_value_is_null()
    {
        // In memory the fold calls string.ToLower() on the selected value; a null column selection
        // therefore throws (EF Core would instead translate this to a SQL NULL comparison).
        var predicate = TextSearch.Contains<Row>(r => r.Name, TextSearch.Normalize("x")).Compile();

        var act = () => predicate(new Row(null));

        act.Should().Throw<NullReferenceException>();
    }
}
