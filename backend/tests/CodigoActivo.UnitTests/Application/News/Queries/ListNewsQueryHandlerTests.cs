using AwesomeAssertions;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class ListNewsQueryHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly TestClock clock = new();
    private readonly ListNewsQueryHandler sut;

    public ListNewsQueryHandlerTests()
    {
        sut = new ListNewsQueryHandler(news, new FakeQueryExecutor(), clock);
    }

    [Fact]
    public async Task HandleAsyncYearFilterReturnsMatchingYear()
    {
        news.HasNews(NewNewsItem("Old", year: 2023), NewNewsItem("New", year: 2025));

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("New");
    }

    [Fact]
    public async Task HandleAsyncYearOutOfRangeReturnsEmpty()
    {
        news.HasNews(NewNewsItem("Any", year: 2025));

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
        news.HasNews(NewNewsItem("Antiguo", year: 2025), NewNewsItem("Futuro", year: 9999));

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Year = 9999 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(a => a.Title == "Futuro");
    }

    [Fact]
    public async Task HandleAsyncCreatedRangeFilterKeepsNewsWithinDayBounds()
    {
        news.HasNews(
            NewNewsItem(
                "Antes",
                createdAt: new DateTimeOffset(2024, 5, 9, 23, 59, 0, TimeSpan.Zero)
            ),
            NewNewsItem(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewNewsItem(
                "Despues",
                createdAt: new DateTimeOffset(2024, 5, 11, 0, 0, 0, TimeSpan.Zero)
            )
        );

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
        news.HasNews(
            NewNewsItem(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 21, 0, 0, TimeSpan.Zero)
            ),
            NewNewsItem(
                "Fuera",
                createdAt: new DateTimeOffset(2024, 5, 10, 23, 0, 0, TimeSpan.Zero)
            )
        );

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
        news.HasNews(NewNewsItem("Star", featured: true), NewNewsItem("Plain", featured: false));

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Featured = featured }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsyncTitleSearchIsAccentAndCaseInsensitive()
    {
        news.HasNews(NewNewsItem("Reunión Ávila"), NewNewsItem("Otra"));

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task HandleAsyncSubtitleSearchMatchesSubstring()
    {
        news.HasNews(
            NewNewsItem("A", subtitle: "primavera"),
            NewNewsItem("B", subtitle: "invierno")
        );

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Subtitle = "vera" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsyncSearchMatchesTitleOrSubtitle()
    {
        news.HasNews(
            NewNewsItem("Inscripciones abiertas", subtitle: "Verano"),
            NewNewsItem("Novedades", subtitle: "Nuevas inscripciones"),
            NewNewsItem("Resultados", subtitle: "Torneo")
        );

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
        news.HasNews(
            NewNewsItem("Reunión anual", year: 2024),
            NewNewsItem("Reunión de socios", year: 2025),
            NewNewsItem("Calendario", year: 2025)
        );

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Search = "reunion", Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión de socios");
    }

    [Fact]
    public async Task HandleAsyncExplicitTitleSortOrdersAscending()
    {
        news.HasNews(NewNewsItem("Charlie"), NewNewsItem("Alpha"), NewNewsItem("Bravo"));

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery { Sort = "title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task HandleAsyncEqualCreatedAtOrdersByIdTieBreakForStablePagination()
    {
        var first = NewNewsItem("First");
        first.Id = new Guid("00000001-0000-0000-0000-000000000000");
        var second = NewNewsItem("Second");
        second.Id = new Guid("00000002-0000-0000-0000-000000000000");
        var third = NewNewsItem("Third");
        third.Id = new Guid("00000003-0000-0000-0000-000000000000");
        news.HasNews(third, first, second);

        var result = await sut.HandleAsync(
            new ListNewsQuery(new NewsListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Id).Should().ContainInOrder(first.Id, second.Id, third.Id);
    }
}
