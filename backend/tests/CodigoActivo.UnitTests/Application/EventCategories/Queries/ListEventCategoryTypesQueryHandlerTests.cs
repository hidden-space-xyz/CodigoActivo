using AwesomeAssertions;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Application.EventCategories.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.EventCategories.Queries;

public sealed class ListEventCategoryTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListEventCategoryTypesQueryHandler sut;

    public ListEventCategoryTypesQueryHandlerTests()
    {
        sut = new ListEventCategoryTypesQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncDefaultSortOrdersByNameAscending()
    {
        store.EventCategoryTypes.AddRange([
            NewCategoryTypeRow("Zeta"),
            NewCategoryTypeRow("Alpha"),
            NewCategoryTypeRow("Mint"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(c => c.Name).Should().ContainInOrder("Alpha", "Mint", "Zeta");
    }

    [Fact]
    public async Task HandleAsyncNameFilterIsAccentAndCaseInsensitive()
    {
        store.EventCategoryTypes.AddRange([
            NewCategoryTypeRow("Robótica"),
            NewCategoryTypeRow("Charlas"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Name = "ROBOTICA" }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Robótica");
    }

    [Fact]
    public async Task HandleAsyncColorFilterMatchesSubstringCaseInsensitively()
    {
        store.EventCategoryTypes.AddRange([
            NewCategoryTypeRow("Talleres", color: "#AABB11"),
            NewCategoryTypeRow("Charlas", color: "#22CC33"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Color = "aabb" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Talleres");
    }

    [Fact]
    public async Task HandleAsyncSortByColorOrdersByColor()
    {
        store.EventCategoryTypes.AddRange([
            NewCategoryTypeRow("Tercero", color: "#333333"),
            NewCategoryTypeRow("Primero", color: "#111111"),
            NewCategoryTypeRow("Segundo", color: "#222222"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventCategoryTypesQuery(new EventCategoryTypeListQuery { Sort = "color" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(c => c.Name).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsyncSecondPageReturnsRemainingItemsWithTotal()
    {
        store.EventCategoryTypes.AddRange([
            NewCategoryTypeRow("Alpha"),
            NewCategoryTypeRow("Mint"),
            NewCategoryTypeRow("Zeta"),
        ]);

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
