using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetHouseholdAssignmentsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetHouseholdAssignmentsQueryHandler sut;

    public GetHouseholdAssignmentsQueryHandlerTests()
    {
        sut = new GetHouseholdAssignmentsQueryHandler(store, new FakeQueryExecutor());
    }

    private static UserRow HouseholdUser(
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

    private static AssignmentRow HouseholdAssignment(
        UserRow user,
        Guid eventId,
        int startHour = 10,
        string roleName = "Participante",
        string statusName = "Solicitado"
    )
    {
        var activity = OverlapActivityRow(
            Guid.NewGuid(),
            startHour,
            startHour + 1,
            eventId: eventId
        );
        return new AssignmentRow
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleTypeRow { Name = roleName, Description = "d" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusTypeRow
            {
                Description = "Descripción de prueba",
                Name = statusName,
                Color = "#000",
            },
        };
    }

    [Fact]
    public async Task HandleAsyncParentAndChildAssignedOrdersByFirstNameAndIncludesChild()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var child = HouseholdUser(Guid.NewGuid(), "Ana", "Kid", actingUserId);
        store.Assignments.AddRange([
            HouseholdAssignment(parent, eventId, roleName: "Líder", statusName: "Confirmado"),
            HouseholdAssignment(child, eventId),
        ]);

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
    public async Task HandleAsyncSameUserMultipleActivitiesOrdersByActivityStart()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var late = HouseholdAssignment(parent, eventId, startHour: 15);
        var early = HouseholdAssignment(parent, eventId, startHour: 9);
        store.Assignments.AddRange([late, early]);

        var result = await sut.HandleAsync(
            new GetHouseholdAssignmentsQuery(actingUserId, eventId),
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.ActivityId).Should().Equal(early.ActivityId, late.ActivityId);
    }

    [Fact]
    public async Task HandleAsyncStrangerOrOtherEventAssignmentsAreExcluded()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var stranger = HouseholdUser(Guid.NewGuid(), "Bob", "Stranger");
        var mine = HouseholdAssignment(parent, eventId);
        store.Assignments.AddRange([
            mine,
            HouseholdAssignment(parent, Guid.NewGuid()),
            HouseholdAssignment(stranger, eventId),
        ]);

        var result = await sut.HandleAsync(
            new GetHouseholdAssignmentsQuery(actingUserId, eventId),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0].ActivityId.Should().Be(mine.ActivityId);
    }
}
