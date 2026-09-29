using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class ChangeAssignmentRoleCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ChangeAssignmentRoleCommandHandler sut;

    public ChangeAssignmentRoleCommandHandlerTests()
    {
        sut = new ChangeAssignmentRoleCommandHandler(
            activities,
            readStore,
            new FakeQueryExecutor(),
            uow,
            cacheInvalidator
        );
    }

    private void RoleExists(Guid roleTypeId, bool exists)
    {
        readStore.ActivityRoleTypes.Add(
            new ActivityRoleTypeRow
            {
                Id = exists ? roleTypeId : Guid.NewGuid(),
                Name = "Rol",
                Description = "d",
            }
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activity.Id,
                Guid.NewGuid(),
                new ChangeAssignmentRoleRequest(Guid.NewGuid())
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRoleTypeMissingReturnsRoleTypeNotFound()
    {
        var userId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var activity = NewActivity();
        activity.SignUp(userId);
        activities.Finds(activity);
        RoleExists(roleTypeId, false);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activity.Id,
                userId,
                new ChangeAssignmentRoleRequest(roleTypeId)
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesRolePersistsAndInvalidatesCache()
    {
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Leader;
        var activity = NewActivity();
        var assignment = activity.SignUp(userId);
        activities.Finds(activity);
        RoleExists(roleId, true);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activity.Id,
                userId,
                new ChangeAssignmentRoleRequest(roleId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                MatchesAssignment(a, userId, activity.Id, roleId, assignment.AssignmentStatusId)
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Activities)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncSameRoleAsCurrentKeepsAssignmentWithoutRemovingOrSaving()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var activity = NewActivity();
        var assignment = activity.SignUp(userId, roleId, statusId);
        activities.Finds(activity);
        RoleExists(roleId, true);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activity.Id,
                userId,
                new ChangeAssignmentRoleRequest(roleId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        assignment.ActivityRoleTypeId.Should().Be(roleId);
        assignment.AssignmentStatusId.Should().Be(statusId);
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(assignment);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }
}
