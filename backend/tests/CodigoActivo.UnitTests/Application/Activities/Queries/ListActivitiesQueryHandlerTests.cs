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
    public async Task HandleAsyncModalityTypeIdFilterReturnsMatchingActivity()
    {
        var modalityId = Guid.NewGuid();
        store.Activities.AddRange([
            NewActivityRow("En sala", modalityId: modalityId),
            NewActivityRow("En remoto", modalityName: "Online"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { ModalityTypeId = modalityId }),
            TestContext.Current.CancellationToken
        );

        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("En sala");
        item.ModalityId.Should().Be(modalityId);
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
    public async Task HandleAsyncLocationSearchIsAccentAndCaseInsensitive()
    {
        store.Activities.AddRange([
            NewActivityRow("Con acento", location: "Salón Ávila"),
            NewActivityRow("Otra", location: "Patio"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Location = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Con acento");
    }

    [Fact]
    public async Task HandleAsyncActivityDateRangeFilterKeepsActivitiesOverlappingRange()
    {
        store.Activities.AddRange([
            NewActivityRow(
                "Antes",
                startsAt: new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivityRow(
                "Dentro",
                startsAt: new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivityRow(
                "Despues",
                startsAt: new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(
                new ActivityListQuery
                {
                    ActivityDateFrom = new DateOnly(2026, 7, 10),
                    ActivityDateTo = new DateOnly(2026, 7, 10),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
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

    [Fact]
    public async Task HandleAsyncSortByLocationOrdersByLocation()
    {
        store.Activities.AddRange([
            NewActivityRow("Ultima", location: "Zaguán"),
            NewActivityRow("Primera", location: "Aula"),
            NewActivityRow("Segunda", location: "Mercado"),
        ]);

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Sort = "location" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Location).Should().ContainInOrder("Aula", "Mercado", "Zaguán");
    }
}
