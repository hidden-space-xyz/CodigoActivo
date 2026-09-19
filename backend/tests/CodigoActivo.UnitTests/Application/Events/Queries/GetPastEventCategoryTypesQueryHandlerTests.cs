using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetPastEventCategoryTypesQueryHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestClock clock = new();
    private readonly GetPastEventCategoryTypesQueryHandler sut;

    public GetPastEventCategoryTypesQueryHandlerTests()
    {
        sut = new GetPastEventCategoryTypesQueryHandler(
            categoryTypes,
            new FakeQueryExecutor(),
            clock
        );
    }

    private static EventCategoryType WithEvents(
        EventCategoryType categoryType,
        params Event[] events
    )
    {
        foreach (var ev in events)
        {
            categoryType.Events.Add(
                new EventCategory
                {
                    EventId = ev.Id,
                    Event = ev,
                    EventCategoryTypeId = categoryType.Id,
                    EventCategoryType = categoryType,
                }
            );
        }

        return categoryType;
    }

    [Fact]
    public async Task HandleAsyncMixedEventsReturnsCategoriesOfPastEventsOrderedByName()
    {
        clock.Today = new DateOnly(2026, 7, 4);
        var past = NewEvent(
            "Pasado",
            starts: new DateOnly(2026, 1, 1),
            ends: new DateOnly(2026, 1, 2)
        );
        var endsToday = NewEvent(
            "Hoy",
            starts: new DateOnly(2026, 7, 3),
            ends: new DateOnly(2026, 7, 4)
        );
        var upcoming = NewEvent(
            "Futuro",
            starts: new DateOnly(2026, 8, 1),
            ends: new DateOnly(2026, 8, 2)
        );
        categoryTypes.HasCategoryTypes(
            WithEvents(NewCategoryType("Talleres", "#AA0000"), past, upcoming),
            WithEvents(NewCategoryType("Charlas", "#00AA00"), past),
            WithEvents(NewCategoryType("Música"), endsToday, upcoming),
            NewCategoryType("Sin eventos")
        );

        var result = await sut.HandleAsync(
            new GetPastEventCategoryTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(c => c.Name).Should().Equal("Charlas", "Talleres");
        result[0].Color.Should().Be("#00AA00");
    }
}
