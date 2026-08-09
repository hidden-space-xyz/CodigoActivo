using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class UnassignActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UnassignActivityCommandHandler sut;

    public UnassignActivityCommandHandlerTests()
    {
        sut = new UnassignActivityCommandHandler(
            activities,
            new SignupGate(activities, users, new FakeQueryExecutor(), clock),
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        activities.ExistingAssignment(null);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(Guid.NewGuid(), Guid.NewGuid(), IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAsAdminRemovesWithoutWindowCheckAndInvalidatesCache()
    {
        var assignment = Assignment(Guid.NewGuid(), Guid.NewGuid());
        activities.ExistingAssignment(assignment);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(assignment.ActivityId, assignment.UserId, IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
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
    public async Task HandleAsyncWindowClosedForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        clock.UtcNow = Now;
        activities.ExistingAssignment(assignment);
        activities.HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activityId, assignment.UserId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        activities.DidNotReceiveWithAnyArgs().RemoveAssignment(new ActivityUserRoleAssignment());
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncWindowOpenForMemberRemovesAssignment()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        clock.UtcNow = Now;
        activities.ExistingAssignment(assignment);
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activityId, assignment.UserId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
