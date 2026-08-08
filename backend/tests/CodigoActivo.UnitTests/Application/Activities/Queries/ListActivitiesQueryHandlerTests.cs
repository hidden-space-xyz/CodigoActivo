using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivitiesQueryHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly TestClock clock = new();
    private readonly ListActivitiesQueryHandler sut;

    public ListActivitiesQueryHandlerTests()
    {
        sut = new ListActivitiesQueryHandler(
            activities,
            new FakeQueryExecutor(),
            clock,
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_EventIdFilter_ReturnsMatchingActivity()
    {
        var eventId = Guid.NewGuid();
        activities.HasActivities(NewActivity("Mine", eventId: eventId), NewActivity("Other"));

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { EventId = eventId }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Mine");
    }

    [Fact]
    public async Task HandleAsync_TitleSearch_IsAccentAndCaseInsensitive()
    {
        activities.HasActivities(NewActivity("Reunión Ávila"), NewActivity("Banco"));

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task HandleAsync_ExplicitDescendingSort_OrdersDescending()
    {
        activities.HasActivities(NewActivity("Alpha"), NewActivity("Zeta"), NewActivity("Mint"));

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Sort = "-title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task HandleAsync_ModalityTypeIdFilter_ReturnsMatchingActivity()
    {
        var modalityId = Guid.NewGuid();
        activities.HasActivities(
            NewActivity("En sala", modalityId: modalityId),
            NewActivity("En remoto", modalityName: "Online")
        );

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { ModalityTypeId = modalityId }),
            TestContext.Current.CancellationToken
        );

        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("En sala");
        item.ModalityId.Should().Be(modalityId);
    }

    [Fact]
    public async Task HandleAsync_SortByModalityName_OrdersByModalityName()
    {
        activities.HasActivities(
            NewActivity("Tercera", modalityName: "Presencial"),
            NewActivity("Primera", modalityName: "Híbrida"),
            NewActivity("Segunda", modalityName: "Online")
        );

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
    public async Task HandleAsync_LocationSearch_IsAccentAndCaseInsensitive()
    {
        activities.HasActivities(
            NewActivity("Con acento", location: "Salón Ávila"),
            NewActivity("Otra", location: "Patio")
        );

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Location = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Con acento");
    }

    [Fact]
    public async Task HandleAsync_ActivityDateRangeFilter_KeepsActivitiesOverlappingRange()
    {
        activities.HasActivities(
            NewActivity(
                "Antes",
                startsAt: new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Dentro",
                startsAt: new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Despues",
                startsAt: new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
            )
        );

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
    public async Task HandleAsync_ActivityDateFromFilter_UsesAppTimeZoneDayStart()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        activities.HasActivities(
            NewActivity(
                "Madrugada",
                startsAt: new DateTimeOffset(2026, 7, 9, 22, 30, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 23, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Anterior",
                startsAt: new DateTimeOffset(2026, 7, 9, 20, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 21, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(
                new ActivityListQuery { ActivityDateFrom = new DateOnly(2026, 7, 10) }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Madrugada");
    }

    [Fact]
    public async Task HandleAsync_SortByLocation_OrdersByLocation()
    {
        activities.HasActivities(
            NewActivity("Ultima", location: "Zaguán"),
            NewActivity("Primera", location: "Aula"),
            NewActivity("Segunda", location: "Mercado")
        );

        var result = await sut.HandleAsync(
            new ListActivitiesQuery(new ActivityListQuery { Sort = "location" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Location).Should().ContainInOrder("Aula", "Mercado", "Zaguán");
    }
}
