using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivitiesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly ListActivitiesQueryHandler sut;

    public ListActivitiesQueryHandlerTests()
    {
        sut = new ListActivitiesQueryHandler(store, new FakeQueryExecutor(), clock);
    }

    [Fact]
    public async Task HandleAsyncEventIdFilterReturnsMatchingActivity()
    {
        var eventId = Guid.NewGuid();
        store.Activities.AddRange([
            NewActivityRow("Mine", eventId: eventId),
            NewActivityRow("Other"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { EventId = eventId }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Mine");
    }

    [Fact]
    public async Task HandleAsyncTitleSearchIsAccentAndCaseInsensitive()
    {
        store.Activities.AddRange([NewActivityRow("Reunión Ávila"), NewActivityRow("Banco")]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task HandleAsyncExplicitDescendingSortOrdersDescending()
    {
        store.Activities.AddRange([
            NewActivityRow("Alpha"),
            NewActivityRow("Zeta"),
            NewActivityRow("Mint"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Sort = "-title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task HandleAsyncSortByModalityNameOrdersByModalityName()
    {
        store.Activities.AddRange([
            NewActivityRow("Tercera", modalityName: "Presencial"),
            NewActivityRow("Primera", modalityName: "Híbrida"),
            NewActivityRow("Segunda", modalityName: "Online"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Sort = "modalityName" }),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(a => a.ModalityName)
            .Should()
            .ContainInOrder("Híbrida", "Online", "Presencial");
    }

    [Fact]
    public async Task HandleAsyncActivityDateFromFilterUsesAppTimeZoneDayStart()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        store.Activities.AddRange([
            NewActivityRow(
                "Madrugada",
                startsAt: new DateTimeOffset(2026, 7, 9, 22, 30, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 23, 0, 0, TimeSpan.Zero)
            ),
            NewActivityRow(
                "Anterior",
                startsAt: new DateTimeOffset(2026, 7, 9, 20, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 21, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(
                new ActivityListQuery { ActivityDateFrom = new DateOnly(2026, 7, 10) }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Madrugada");
    }
}
