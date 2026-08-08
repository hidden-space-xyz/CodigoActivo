using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListAssignedActivitiesQueryHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly ListAssignedActivitiesQueryHandler sut;

    public ListAssignedActivitiesQueryHandlerTests()
    {
        sut = new ListAssignedActivitiesQueryHandler(activities, new FakeQueryExecutor());
    }

    private static ActivityUserRoleAssignment Assignment(
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
            Activity = new Activity
            {
                Location = "Sala principal",
                Title = title,
                Description = "{}",
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt.AddHours(1),
                EventId = eventId ?? Guid.NewGuid(),
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType
            {
                Description = "Descripción de prueba",
                Name = "Líder",
            },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType
            {
                Description = "Descripción de prueba",
                Name = "Solicitado",
                Color = "#000",
            },
        };
    }

    [Fact]
    public async Task HandleAsync_MultipleUsersAssigned_FiltersByUserAndOrdersByStart()
    {
        var userId = Guid.NewGuid();
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    Assignment(
                        userId,
                        "Late",
                        new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero)
                    ),
                    Assignment(
                        userId,
                        "Early",
                        new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)
                    ),
                    Assignment(
                        Guid.NewGuid(),
                        "Other",
                        new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)
                    ),
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new ListAssignedActivitiesQuery(userId, EventId: null),
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.Title).Should().ContainInOrder("Early", "Late");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_EventIdFilter_ExcludesOtherEventAssignments()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
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
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new ListAssignedActivitiesQuery(userId, eventId),
            TestContext.Current.CancellationToken
        );

        var assigned = result.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Mine");
        assigned.EventId.Should().Be(eventId);
    }
}
