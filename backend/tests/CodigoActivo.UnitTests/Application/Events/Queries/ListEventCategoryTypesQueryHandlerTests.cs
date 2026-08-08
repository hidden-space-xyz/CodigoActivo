using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class ListEventCategoryTypesQueryHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly ListEventCategoryTypesQueryHandler sut;

    public ListEventCategoryTypesQueryHandlerTests()
    {
        sut = new ListEventCategoryTypesQueryHandler(
            categoryTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_DefaultSort_OrdersByNameAscending()
    {
        categoryTypes.HasCategoryTypes(
            NewCategoryType("Zeta"),
            NewCategoryType("Alpha"),
            NewCategoryType("Mint")
        );

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(c => c.Name).Should().ContainInOrder("Alpha", "Mint", "Zeta");
    }

    [Fact]
    public async Task HandleAsync_NameFilter_IsAccentAndCaseInsensitive()
    {
        categoryTypes.HasCategoryTypes(NewCategoryType("Robótica"), NewCategoryType("Charlas"));

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Name = "ROBOTICA" }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Robótica");
    }

    [Fact]
    public async Task HandleAsync_ColorFilter_MatchesSubstringCaseInsensitively()
    {
        categoryTypes.HasCategoryTypes(
            NewCategoryType("Talleres", color: "#AABB11"),
            NewCategoryType("Charlas", color: "#22CC33")
        );

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Color = "aabb" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Talleres");
    }

    [Fact]
    public async Task HandleAsync_SortByColor_OrdersByColor()
    {
        categoryTypes.HasCategoryTypes(
            NewCategoryType("Tercero", color: "#333333"),
            NewCategoryType("Primero", color: "#111111"),
            NewCategoryType("Segundo", color: "#222222")
        );

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Sort = "color" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(c => c.Name).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsync_SecondPage_ReturnsRemainingItemsWithTotal()
    {
        categoryTypes.HasCategoryTypes(
            NewCategoryType("Alpha"),
            NewCategoryType("Mint"),
            NewCategoryType("Zeta")
        );

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(
                new EventCategoryTypeListQuery { Page = 2, PageSize = 2 }
            ),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Zeta");
    }
}
