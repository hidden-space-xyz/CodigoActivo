using AwesomeAssertions;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class ListEventsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly ListEventsQueryHandler sut;

    public ListEventsQueryHandlerTests()
    {
        sut = new ListEventsQueryHandler(store, new FakeQueryExecutor(), clock);
    }

    [Fact]
    public async Task HandleAsyncNoScopeProjectsAndPagesAll()
    {
        store.Events.AddRange([NewEventRow("A"), NewEventRow("B")]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Page = 1, PageSize = 10 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllBeOfType<EventListItemResponse>();
    }

    [Theory]
    [InlineData(EventScope.Upcoming, "Upcoming")]
    [InlineData(EventScope.Past, "Past")]
    public async Task HandleAsyncScopeKeepsEventsMatchingScope(
        EventScope scope,
        string expectedTitle
    )
    {
        clock.Today = new DateOnly(2026, 7, 4);
        store.Events.AddRange([
            NewEventRow("Past", starts: new DateOnly(2026, 1, 1), ends: new DateOnly(2026, 1, 2)),
            NewEventRow(
                "Upcoming",
                starts: new DateOnly(2026, 8, 1),
                ends: new DateOnly(2026, 8, 2)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Scope = scope }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public async Task HandleAsyncYearFilterKeepsMatchingYear()
    {
        store.Events.AddRange([
            NewEventRow("Y2025", starts: new DateOnly(2025, 5, 1), ends: new DateOnly(2025, 5, 2)),
            NewEventRow("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2)),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Y2025");
    }

    [Fact]
    public async Task HandleAsyncYearOutOfRangeReturnsEmpty()
    {
        store.Events.AddRange([
            NewEventRow("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2)),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 0 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncYearMaximumSupportedReturnsEventsOfYear9999()
    {
        store.Events.AddRange([
            NewEventRow("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2)),
            NewEventRow(
                "Y9999",
                starts: new DateOnly(9999, 6, 15),
                ends: new DateOnly(9999, 6, 16)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 9999 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(e => e.Title == "Y9999");
    }

    [Fact]
    public async Task HandleAsyncCategoryTypeIdFilterKeepsEventsWithMatchingCategory()
    {
        var categoryId = Guid.NewGuid();
        store.Events.AddRange([
            WithCategory(NewEventRow("Con"), categoryId, "Talleres"),
            WithCategory(NewEventRow("Sin"), Guid.NewGuid(), "Charlas"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { CategoryTypeId = categoryId }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Con");
    }

    [Fact]
    public async Task HandleAsyncEventDateRangeFilterKeepsEventsOverlappingRange()
    {
        store.Events.AddRange([
            NewEventRow("Antes", starts: new DateOnly(2026, 3, 1), ends: new DateOnly(2026, 3, 2)),
            NewEventRow(
                "Solapa",
                starts: new DateOnly(2026, 4, 28),
                ends: new DateOnly(2026, 5, 2)
            ),
            NewEventRow(
                "Dentro",
                starts: new DateOnly(2026, 5, 10),
                ends: new DateOnly(2026, 5, 11)
            ),
            NewEventRow(
                "Despues",
                starts: new DateOnly(2026, 6, 1),
                ends: new DateOnly(2026, 6, 2)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(
                new EventListQuery
                {
                    EventDateFrom = new DateOnly(2026, 5, 1),
                    EventDateTo = new DateOnly(2026, 5, 31),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().BeEquivalentTo("Solapa", "Dentro");
    }

    [Fact]
    public async Task HandleAsyncSignupFromFilterUsesAppTimeZoneDayStart()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        store.Events.AddRange([
            NewEventRow(
                "EnLimite",
                signupEnd: new DateTimeOffset(2026, 7, 19, 23, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Cerrado",
                signupEnd: new DateTimeOffset(2026, 7, 19, 21, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { SignupFrom = new DateOnly(2026, 7, 20) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("EnLimite");
    }

    [Fact]
    public async Task HandleAsyncSignupToFilterExcludesSignupsStartingAfterDayEnd()
    {
        store.Events.AddRange([
            NewEventRow(
                "Abierto",
                signupStart: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Futuro",
                signupStart: new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { SignupTo = new DateOnly(2026, 7, 10) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Abierto");
    }

    [Fact]
    public async Task HandleAsyncSortBySignupStartsAtOrdersBySignupStart()
    {
        store.Events.AddRange([
            NewEventRow(
                "Tercero",
                signupStart: new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Primero",
                signupStart: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Segundo",
                signupStart: new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "signupStartsAt" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsyncSortBySignupEndsAtDescendingOrdersBySignupEndDescending()
    {
        store.Events.AddRange([
            NewEventRow(
                "Medio",
                signupEnd: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Ultimo",
                signupEnd: new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEventRow(
                "Primero",
                signupEnd: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "-signupEndsAt" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Ultimo", "Medio", "Primero");
    }

    [Fact]
    public async Task HandleAsyncSortByCategoriesOrdersByMinimumCategoryName()
    {
        var second = WithCategory(NewEventRow("Segundo"), Guid.NewGuid(), "Charlas");
        WithCategory(second, Guid.NewGuid(), "Zumba");
        var first = WithCategory(NewEventRow("Primero"), Guid.NewGuid(), "Ajedrez");
        var third = WithCategory(NewEventRow("Tercero"), Guid.NewGuid(), "Mercadillo");
        store.Events.AddRange([second, first, third]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "categories" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsyncFeaturedFilterKeepsFeaturedOnly()
    {
        store.Events.AddRange([
            NewEventRow("Plain", featured: false),
            NewEventRow("Star", featured: true),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Featured = true }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Star");
    }

    [Fact]
    public async Task HandleAsyncTitleSearchIsAccentAndCaseInsensitive()
    {
        store.Events.AddRange([NewEventRow("Festival Ávila"), NewEventRow("Concierto")]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Festival Ávila");
    }

    [Fact]
    public async Task HandleAsyncSubtitleSearchMatchesSubstring()
    {
        store.Events.AddRange([
            NewEventRow("A", subtitle: "Talleres de robótica"),
            NewEventRow("B", subtitle: "Charlas"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Subtitle = "robotica" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsyncSearchMatchesTitleOrSubtitle()
    {
        store.Events.AddRange([
            NewEventRow("Robótica creativa", subtitle: "Taller"),
            NewEventRow("Campus", subtitle: "Talleres de robótica"),
            NewEventRow("Ajedrez", subtitle: "Torneo"),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Search = " ROBOTICA " }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().BeEquivalentTo("Robótica creativa", "Campus");
    }

    [Fact]
    public async Task HandleAsyncSearchCombinesWithYearAndCategoryFilters()
    {
        var categoryId = Guid.NewGuid();
        store.Events.AddRange([
            WithCategory(
                NewEventRow(
                    "Robótica 2025",
                    starts: new DateOnly(2025, 5, 1),
                    ends: new DateOnly(2025, 5, 2)
                ),
                categoryId,
                "Talleres"
            ),
            WithCategory(
                NewEventRow(
                    "Robótica 2024",
                    starts: new DateOnly(2024, 5, 1),
                    ends: new DateOnly(2024, 5, 2)
                ),
                categoryId,
                "Talleres"
            ),
            WithCategory(
                NewEventRow(
                    "Robótica charla",
                    starts: new DateOnly(2025, 6, 1),
                    ends: new DateOnly(2025, 6, 2)
                ),
                Guid.NewGuid(),
                "Charlas"
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(
                new EventListQuery
                {
                    Search = "robotica",
                    Year = 2025,
                    CategoryTypeId = categoryId,
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Robótica 2025");
    }

    [Fact]
    public async Task HandleAsyncDescendingTitleSortOrdersResults()
    {
        store.Events.AddRange([NewEventRow("Alpha"), NewEventRow("Zeta"), NewEventRow("Mint")]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "-title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task HandleAsyncSecondPageSkipsFirstPageItems()
    {
        store.Events.AddRange([NewEventRow("Alpha"), NewEventRow("Mint"), NewEventRow("Zeta")]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(
                new EventListQuery
                {
                    Page = 2,
                    PageSize = 2,
                    Sort = "title",
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("Zeta");
    }
}
