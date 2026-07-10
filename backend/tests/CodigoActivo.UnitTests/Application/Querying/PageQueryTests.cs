using AwesomeAssertions;
using CodigoActivo.Application.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

public sealed class PageQueryTests
{
    private sealed class TestPageQuery : PageQuery;

    [Fact]
    public void PageQuery_NewInstance_DefaultsToPageOneDefaultSizeNullSort()
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
    public void Page_BelowOneOrValid_ClampsToOneOrPassesThrough(int input, int expected)
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
    public void PageSize_OutOfRangeOrValid_ClampsToBoundsOrPassesThrough(int input, int expected)
    {
        var query = new TestPageQuery { PageSize = input };

        query.PageSize.Should().Be(expected);
    }
}
