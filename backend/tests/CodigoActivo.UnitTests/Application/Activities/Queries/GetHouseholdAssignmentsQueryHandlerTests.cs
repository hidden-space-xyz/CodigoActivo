using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetHouseholdAssignmentsQueryHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly GetHouseholdAssignmentsQueryHandler sut;

    public GetHouseholdAssignmentsQueryHandlerTests()
    {
        sut = new GetHouseholdAssignmentsQueryHandler(activities, new FakeQueryExecutor());
    }

    private static User HouseholdUser(
        Guid id,
        string firstName,
        string lastName,
        Guid? parentId = null
    )
    {
        return new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            ParentId = parentId,
        };
    }

    private static ActivityUserRoleAssignment HouseholdAssignment(
        User user,
        Guid eventId,
        int startHour = 10,
        string roleName = "Participante",
        string statusName = "Solicitado"
    )
    {
        var activity = OverlapActivity(Guid.NewGuid(), startHour, startHour + 1);
        activity.EventId = eventId;
        return new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType { Name = roleName, Description = "d" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType
            {
                Description = "Descripción de prueba",
                Name = statusName,
                Color = "#000",
            },
        };
    }

    [Fact]
    public async Task HandleAsync_ParentAndChildAssigned_OrdersByFirstNameAndIncludesChild()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var child = HouseholdUser(Guid.NewGuid(), "Ana", "Kid", actingUserId);
        activities.HasAssignments(
            HouseholdAssignment(parent, eventId, roleName: "Líder", statusName: "Confirmado"),
            HouseholdAssignment(child, eventId)
        );

        var result = await sut.HandleAsync(
            new GetHouseholdAssignmentsQuery(actingUserId, eventId),
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(child.Id);
        result[0].FirstName.Should().Be("Ana");
        result[0].LastName.Should().Be("Kid");
        result[0].RoleName.Should().Be("Participante");
        result[0].StatusName.Should().Be("Solicitado");
        result[1].UserId.Should().Be(actingUserId);
        result[1].FirstName.Should().Be("Zoe");
        result[1].RoleName.Should().Be("Líder");
        result[1].StatusName.Should().Be("Confirmado");
    }

    [Fact]
    public async Task HandleAsync_SameUserMultipleActivities_OrdersByActivityStart()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var late = HouseholdAssignment(parent, eventId, startHour: 15);
        var early = HouseholdAssignment(parent, eventId, startHour: 9);
        activities.HasAssignments(late, early);

        var result = await sut.HandleAsync(
            new GetHouseholdAssignmentsQuery(actingUserId, eventId),
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.ActivityId).Should().Equal(early.ActivityId, late.ActivityId);
    }

    [Fact]
    public async Task HandleAsync_StrangerOrOtherEventAssignments_AreExcluded()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var stranger = HouseholdUser(Guid.NewGuid(), "Bob", "Stranger");
        var mine = HouseholdAssignment(parent, eventId);
        activities.HasAssignments(
            mine,
            HouseholdAssignment(parent, Guid.NewGuid()),
            HouseholdAssignment(stranger, eventId)
        );

        var result = await sut.HandleAsync(
            new GetHouseholdAssignmentsQuery(actingUserId, eventId),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0].ActivityId.Should().Be(mine.ActivityId);
    }
}
