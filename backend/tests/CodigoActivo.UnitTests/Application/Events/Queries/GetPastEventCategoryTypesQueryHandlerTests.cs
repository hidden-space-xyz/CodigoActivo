using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetPastEventCategoryTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly GetPastEventCategoryTypesQueryHandler sut;

    public GetPastEventCategoryTypesQueryHandlerTests()
    {
        sut = new GetPastEventCategoryTypesQueryHandler(store, new FakeQueryExecutor(), clock);
    }

    private static EventCategoryTypeRow WithEvents(
        EventCategoryTypeRow categoryType,
        params EventRow[] events
    )
    {
        foreach (var ev in events)
        {
            categoryType.Events.Add(
                new EventCategoryRow
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
        var past = NewEventRow(
            "Pasado",
            starts: new DateOnly(2026, 1, 1),
            ends: new DateOnly(2026, 1, 2)
        );
        var endsToday = NewEventRow(
            "Hoy",
            starts: new DateOnly(2026, 7, 3),
            ends: new DateOnly(2026, 7, 4)
        );
        var upcoming = NewEventRow(
            "Futuro",
            starts: new DateOnly(2026, 8, 1),
            ends: new DateOnly(2026, 8, 2)
        );
        store.EventCategoryTypes.AddRange([
            WithEvents(NewCategoryTypeRow("Talleres", "#AA0000"), past, upcoming),
            WithEvents(NewCategoryTypeRow("Charlas", "#00AA00"), past),
            WithEvents(NewCategoryTypeRow("Música"), endsToday, upcoming),
            NewCategoryTypeRow("Sin eventos"),
        ]);

        var result = await sut.HandleAsync(
            new GetPastEventCategoryTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(c => c.Name).Should().Equal("Charlas", "Talleres");
        result[0].Color.Should().Be("#00AA00");
    }
}
