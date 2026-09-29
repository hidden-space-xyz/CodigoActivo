using AwesomeAssertions;
using CodigoActivo.Application.News.Contracts;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class ListNewsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly ListNewsQueryHandler sut;

    public ListNewsQueryHandlerTests()
    {
        sut = new ListNewsQueryHandler(store, new FakeQueryExecutor(), clock);
    }

    [Fact]
    public async Task HandleAsyncYearFilterReturnsMatchingYear()
    {
        store.News.AddRange([NewNewsItemRow("Old", year: 2023), NewNewsItemRow("New", year: 2025)]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("New");
    }

    [Fact]
    public async Task HandleAsyncYearOutOfRangeReturnsEmpty()
    {
        store.News.AddRange([NewNewsItemRow("Any", year: 2025)]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Year = 0 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncYearMaximumSupportedReturnsNewsOfYear9999()
    {
        store.News.AddRange([
            NewNewsItemRow("Antiguo", year: 2025),
            NewNewsItemRow("Futuro", year: 9999),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Year = 9999 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(a => a.Title == "Futuro");
    }

    [Fact]
    public async Task HandleAsyncCreatedRangeFilterKeepsNewsWithinDayBounds()
    {
        store.News.AddRange([
            NewNewsItemRow(
                "Antes",
                createdAt: new DateTimeOffset(2024, 5, 9, 23, 59, 0, TimeSpan.Zero)
            ),
            NewNewsItemRow(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewNewsItemRow(
                "Despues",
                createdAt: new DateTimeOffset(2024, 5, 11, 0, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(
                new NewsListQuery
                {
                    CreatedFrom = new DateOnly(2024, 5, 10),
                    CreatedTo = new DateOnly(2024, 5, 10),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Fact]
    public async Task HandleAsyncCreatedToFilterUsesAppTimeZoneDayEnd()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        store.News.AddRange([
            NewNewsItemRow(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 21, 0, 0, TimeSpan.Zero)
            ),
            NewNewsItemRow(
                "Fuera",
                createdAt: new DateTimeOffset(2024, 5, 10, 23, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { CreatedTo = new DateOnly(2024, 5, 10) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Theory]
    [InlineData(true, "Star")]
    [InlineData(false, "Plain")]
    public async Task HandleAsyncFeaturedFilterReturnsMatchingFeaturedState(
        bool featured,
        string expected
    )
    {
        store.News.AddRange([
            NewNewsItemRow("Star", featured: true),
            NewNewsItemRow("Plain", featured: false),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Featured = featured }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsyncTitleSearchIsAccentAndCaseInsensitive()
    {
        store.News.AddRange([NewNewsItemRow("Reunión Ávila"), NewNewsItemRow("Otra")]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task HandleAsyncSubtitleSearchMatchesSubstring()
    {
        store.News.AddRange([
            NewNewsItemRow("A", subtitle: "primavera"),
            NewNewsItemRow("B", subtitle: "invierno"),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Subtitle = "vera" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsyncSearchMatchesTitleOrSubtitle()
    {
        store.News.AddRange([
            NewNewsItemRow("Inscripciones abiertas", subtitle: "Verano"),
            NewNewsItemRow("Novedades", subtitle: "Nuevas inscripciones"),
            NewNewsItemRow("Resultados", subtitle: "Torneo"),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Search = "INSCRIPCIONES" }),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(a => a.Title)
            .Should()
            .BeEquivalentTo("Inscripciones abiertas", "Novedades");
    }

    [Fact]
    public async Task HandleAsyncSearchCombinesWithYearFilter()
    {
        store.News.AddRange([
            NewNewsItemRow("Reunión anual", year: 2024),
            NewNewsItemRow("Reunión de socios", year: 2025),
            NewNewsItemRow("Calendario", year: 2025),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Search = "reunion", Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión de socios");
    }

    [Fact]
    public async Task HandleAsyncExplicitTitleSortOrdersAscending()
    {
        store.News.AddRange([
            NewNewsItemRow("Charlie"),
            NewNewsItemRow("Alpha"),
            NewNewsItemRow("Bravo"),
        ]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Sort = "title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task HandleAsyncEqualCreatedAtOrdersByIdTieBreakForStablePagination()
    {
        var first = NewNewsItemRow("First", id: new Guid("00000001-0000-0000-0000-000000000000"));
        var second = NewNewsItemRow("Second", id: new Guid("00000002-0000-0000-0000-000000000000"));
        var third = NewNewsItemRow("Third", id: new Guid("00000003-0000-0000-0000-000000000000"));
        store.News.AddRange([third, first, second]);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Id).Should().ContainInOrder(first.Id, second.Id, third.Id);
    }
}
