using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetAssignmentsQueryHandlerTests
{
    private static readonly Guid ActivityId = new("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly FakeReadStore store = new();
    private readonly GetAssignmentsQueryHandler sut;

    public GetAssignmentsQueryHandlerTests()
    {
        sut = new GetAssignmentsQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<IReadOnlyList<AssignmentResponse>> QueryAsync(params Guid[] userIds)
    {
        return sut.HandleAsync(
            new GetAssignmentsQuery(ActivityId, userIds),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncRequestedUsersReturnsTheirAssignmentsInRequestedOrder()
    {
        var ana = Guid.NewGuid();
        var berto = Guid.NewGuid();
        var carla = Guid.NewGuid();
        store.Assignments.AddRange([
            NewAssignmentRow(ana, ActivityId, roleName: "Líder", statusName: "Confirmada"),
            NewAssignmentRow(berto, ActivityId),
            NewAssignmentRow(carla, ActivityId, roleName: "Voluntario"),
            NewAssignmentRow(Guid.NewGuid(), ActivityId),
            NewAssignmentRow(ana, Guid.NewGuid(), roleName: "Participante"),
        ]);

        var assignments = await QueryAsync(carla, ana, berto);

        assignments.Select(a => a.UserId).Should().Equal(carla, ana, berto);
        assignments.Should().OnlyContain(a => a.ActivityId == ActivityId);
        assignments[1].RoleTypeName.Should().Be("Líder");
        assignments[1].Status.Name.Should().Be("Confirmada");
    }

    [Fact]
    public async Task HandleAsyncUserWithoutAssignmentInTheActivityIsSkipped()
    {
        var ana = Guid.NewGuid();
        var berto = Guid.NewGuid();
        store.Assignments.AddRange([
            NewAssignmentRow(ana, ActivityId),
            NewAssignmentRow(berto, Guid.NewGuid()),
        ]);

        var assignments = await QueryAsync(berto, ana, Guid.NewGuid());

        assignments.Should().ContainSingle().Which.UserId.Should().Be(ana);
    }

    [Fact]
    public async Task HandleAsyncNoUsersRequestedReturnsEmpty()
    {
        store.Assignments.Add(NewAssignmentRow(Guid.NewGuid(), ActivityId));

        var assignments = await QueryAsync();

        assignments.Should().BeEmpty();
    }
}
