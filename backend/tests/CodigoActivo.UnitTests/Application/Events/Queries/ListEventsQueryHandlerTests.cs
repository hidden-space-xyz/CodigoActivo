using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class ListEventsQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly TestClock clock = new();
    private readonly ListEventsQueryHandler sut;

    public ListEventsQueryHandlerTests()
    {
        sut = new ListEventsQueryHandler(
            events,
            new FakeQueryExecutor(),
            clock,
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_NoScope_ProjectsAndPagesAll()
    {
        events.HasEvents(NewEvent("A"), NewEvent("B"));

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
    public async Task HandleAsync_Scope_KeepsEventsMatchingScope(
        EventScope scope,
        string expectedTitle
    )
    {
        clock.Today = new DateOnly(2026, 7, 4);
        events.HasEvents(
            NewEvent("Past", starts: new DateOnly(2026, 1, 1), ends: new DateOnly(2026, 1, 2)),
            NewEvent("Upcoming", starts: new DateOnly(2026, 8, 1), ends: new DateOnly(2026, 8, 2))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Scope = scope }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public async Task HandleAsync_YearFilter_KeepsMatchingYear()
    {
        events.HasEvents(
            NewEvent("Y2025", starts: new DateOnly(2025, 5, 1), ends: new DateOnly(2025, 5, 2)),
            NewEvent("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Y2025");
    }

    [Fact]
    public async Task HandleAsync_YearOutOfRange_ReturnsEmpty()
    {
        events.HasEvents(
            NewEvent("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 0 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_YearMaximumSupported_ReturnsEventsOfYear9999()
    {
        events.HasEvents(
            NewEvent("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2)),
            NewEvent("Y9999", starts: new DateOnly(9999, 6, 15), ends: new DateOnly(9999, 6, 16))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Year = 9999 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(e => e.Title == "Y9999");
    }

    [Fact]
    public async Task HandleAsync_CategoryTypeIdFilter_KeepsEventsWithMatchingCategory()
    {
        var categoryId = Guid.NewGuid();
        events.HasEvents(
            WithCategory(NewEvent("Con"), categoryId, "Talleres"),
            WithCategory(NewEvent("Sin"), Guid.NewGuid(), "Charlas")
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { CategoryTypeId = categoryId }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Con");
    }

    [Fact]
    public async Task HandleAsync_EventDateRangeFilter_KeepsEventsOverlappingRange()
    {
        events.HasEvents(
            NewEvent("Antes", starts: new DateOnly(2026, 3, 1), ends: new DateOnly(2026, 3, 2)),
            NewEvent("Solapa", starts: new DateOnly(2026, 4, 28), ends: new DateOnly(2026, 5, 2)),
            NewEvent("Dentro", starts: new DateOnly(2026, 5, 10), ends: new DateOnly(2026, 5, 11)),
            NewEvent("Despues", starts: new DateOnly(2026, 6, 1), ends: new DateOnly(2026, 6, 2))
        );

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
    public async Task HandleAsync_SignupFromFilter_UsesAppTimeZoneDayStart()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        events.HasEvents(
            NewEvent(
                "EnLimite",
                signupEnd: new DateTimeOffset(2026, 7, 19, 23, 0, 0, TimeSpan.Zero)
            ),
            NewEvent("Cerrado", signupEnd: new DateTimeOffset(2026, 7, 19, 21, 0, 0, TimeSpan.Zero))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { SignupFrom = new DateOnly(2026, 7, 20) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("EnLimite");
    }

    [Fact]
    public async Task HandleAsync_SignupToFilter_ExcludesSignupsStartingAfterDayEnd()
    {
        events.HasEvents(
            NewEvent(
                "Abierto",
                signupStart: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewEvent("Futuro", signupStart: new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { SignupTo = new DateOnly(2026, 7, 10) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Abierto");
    }

    [Fact]
    public async Task HandleAsync_SortBySignupStartsAt_OrdersBySignupStart()
    {
        events.HasEvents(
            NewEvent(
                "Tercero",
                signupStart: new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEvent(
                "Primero",
                signupStart: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            NewEvent("Segundo", signupStart: new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "signupStartsAt" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsync_SortBySignupEndsAtDescending_OrdersBySignupEndDescending()
    {
        events.HasEvents(
            NewEvent("Medio", signupEnd: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)),
            NewEvent("Ultimo", signupEnd: new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero)),
            NewEvent("Primero", signupEnd: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero))
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "-signupEndsAt" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Ultimo", "Medio", "Primero");
    }

    [Fact]
    public async Task HandleAsync_SortByCategories_OrdersByMinimumCategoryName()
    {
        var second = WithCategory(NewEvent("Segundo"), Guid.NewGuid(), "Charlas");
        WithCategory(second, Guid.NewGuid(), "Zumba");
        var first = WithCategory(NewEvent("Primero"), Guid.NewGuid(), "Ajedrez");
        var third = WithCategory(NewEvent("Tercero"), Guid.NewGuid(), "Mercadillo");
        events.HasEvents(second, first, third);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "categories" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public async Task HandleAsync_FeaturedFilter_KeepsFeaturedOnly()
    {
        events.HasEvents(NewEvent("Plain", featured: false), NewEvent("Star", featured: true));

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Featured = true }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Star");
    }

    [Fact]
    public async Task HandleAsync_TitleSearch_IsAccentAndCaseInsensitive()
    {
        events.HasEvents(NewEvent("Festival Ávila"), NewEvent("Concierto"));

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Festival Ávila");
    }

    [Fact]
    public async Task HandleAsync_SubtitleSearch_MatchesSubstring()
    {
        events.HasEvents(
            NewEvent("A", subtitle: "Talleres de robótica"),
            NewEvent("B", subtitle: "Charlas")
        );

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Subtitle = "robotica" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsync_DescendingTitleSort_OrdersResults()
    {
        events.HasEvents(NewEvent("Alpha"), NewEvent("Zeta"), NewEvent("Mint"));

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Sort = "-title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task HandleAsync_SecondPage_SkipsFirstPageItems()
    {
        events.HasEvents(NewEvent("Alpha"), NewEvent("Mint"), NewEvent("Zeta"));

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
