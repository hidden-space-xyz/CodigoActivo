using AwesomeAssertions;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Events;
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
    public async Task HandleAsyncItemsCarryTheStageOfTheClock()
    {
        var finished = NewEventRow("Finished", ends: clock.Today.AddDays(-1));
        var open = NewEventRow(
            "Open",
            ends: clock.Today.AddDays(10),
            signupStart: clock.UtcNow.AddDays(-1),
            signupEnd: clock.UtcNow.AddDays(1)
        );
        store.Events.AddRange([finished, open]);

        var result = await sut.HandleAsync(
            new ListEventsQuery(new EventListQuery { Page = 1, PageSize = 10 }),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(item => (item.Title, item.Stage))
            .Should()
            .BeEquivalentTo([("Finished", EventStage.Finished), ("Open", EventStage.SignupOpen)]);
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
}
