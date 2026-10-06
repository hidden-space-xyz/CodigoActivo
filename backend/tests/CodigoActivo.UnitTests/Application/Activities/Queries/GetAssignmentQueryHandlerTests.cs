using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetAssignmentQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetAssignmentQueryHandler sut;

    public GetAssignmentQueryHandlerTests()
    {
        sut = new GetAssignmentQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<Result<AssignmentResponse>> QueryAsync(Guid activityId, Guid userId)
    {
        return sut.HandleAsync(
            new GetAssignmentQuery(activityId, userId),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentExistsReturnsItsRoleAndStatus()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        store.Assignments.AddRange([
            NewAssignmentRow(Guid.NewGuid(), activityId),
            NewAssignmentRow(userId, Guid.NewGuid()),
            NewAssignmentRow(
                userId,
                activityId,
                KnownIds.ActivityRoleTypes.Volunteer,
                "Voluntario",
                KnownIds.AssignmentStatusTypes.Confirmed,
                "Confirmada"
            ),
        ]);

        var result = await QueryAsync(activityId, userId);

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(
                new AssignmentResponse(
                    userId,
                    activityId,
                    KnownIds.ActivityRoleTypes.Volunteer,
                    "Voluntario",
                    new AssignmentStatusResponse(
                        KnownIds.AssignmentStatusTypes.Confirmed,
                        "Confirmada"
                    )
                )
            );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        store.Assignments.AddRange([
            NewAssignmentRow(Guid.NewGuid(), activityId),
            NewAssignmentRow(userId, Guid.NewGuid()),
        ]);

        var result = await QueryAsync(activityId, userId);

        result.ShouldFail(ErrorKind.NotFound, DomainErrorCode.ActivityAssignmentNotFound);
    }
}
