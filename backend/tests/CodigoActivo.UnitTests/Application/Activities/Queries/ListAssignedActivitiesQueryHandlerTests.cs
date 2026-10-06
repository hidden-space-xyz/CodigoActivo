using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListAssignedActivitiesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListAssignedActivitiesQueryHandler sut;

    public ListAssignedActivitiesQueryHandlerTests()
    {
        sut = new ListAssignedActivitiesQueryHandler(store, new FakeQueryExecutor());
    }

    private static AssignmentRow Assignment(
        Guid userId,
        string title,
        DateTimeOffset startsAt,
        Guid? eventId = null
    )
    {
        return new()
        {
            UserId = userId,
            ActivityId = Guid.NewGuid(),
            Activity = new ActivityRow
            {
                Location = "Sala principal",
                Title = title,
                Description = "{}",
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt.AddHours(1),
                EventId = eventId ?? Guid.NewGuid(),
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleTypeRow
            {
                Description = "Descripción de prueba",
                Name = "Líder",
            },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusTypeRow
            {
                Description = "Descripción de prueba",
                Name = "Solicitado",
                Color = "#000",
            },
        };
    }

    [Fact]
    public async Task HandleAsyncMultipleUsersAssignedFiltersByUserAndOrdersByStart()
    {
        var userId = Guid.NewGuid();
        store.Assignments.AddRange([
            Assignment(userId, "Late", new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero)),
            Assignment(userId, "Early", new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)),
            Assignment(
                Guid.NewGuid(),
                "Other",
                new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListAssignedActivitiesQuery(UserId.From(userId), EventId: null),
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.Title).Should().ContainInOrder("Early", "Late");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsyncEventIdFilterExcludesOtherEventAssignments()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        store.Assignments.AddRange([
            Assignment(
                userId,
                "Mine",
                new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero),
                eventId
            ),
            Assignment(
                userId,
                "OtherEvent",
                new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)
            ),
        ]);

        var result = await sut.HandleAsync(
            new ListAssignedActivitiesQuery(UserId.From(userId), EventId.From(eventId)),
            TestContext.Current.CancellationToken
        );

        var assigned = result.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Mine");
        assigned.EventId.Should().Be(eventId);
    }
}
