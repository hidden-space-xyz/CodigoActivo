using CodigoActivo.Application.Querying;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

/// <summary>
/// Unit tests for the self-clamping <see cref="PageQuery"/> base. Exercised through a concrete
/// subclass since the type is abstract.
/// </summary>
public sealed class PageQueryTests
{
    private sealed class TestPageQuery : PageQuery;

    [Fact]
    public void Defaults_are_page_one_default_page_size_and_null_sort()
    {
        var query = new TestPageQuery();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(PageQuery.DefaultPageSize);
        query.PageSize.Should().Be(25);
        query.Sort.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-100, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(9999, 9999)]
    public void Page_clamps_values_below_one_to_one_and_passes_valid_values(int input, int expected)
    {
        var query = new TestPageQuery { Page = input };

        query.Page.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, PageQuery.DefaultPageSize)]
    [InlineData(-1, PageQuery.DefaultPageSize)]
    [InlineData(1, 1)]
    [InlineData(25, 25)]
    [InlineData(100, PageQuery.MaxPageSize)]
    [InlineData(101, PageQuery.MaxPageSize)]
    [InlineData(5000, PageQuery.MaxPageSize)]
    public void PageSize_clamps_out_of_range_values_and_passes_valid_ones(int input, int expected)
    {
        var query = new TestPageQuery { PageSize = input };

        query.PageSize.Should().Be(expected);
    }

    [Fact]
    public void Sort_round_trips_the_assigned_value()
    {
        var query = new TestPageQuery { Sort = "-createdAt,title" };

        query.Sort.Should().Be("-createdAt,title");
    }
}
