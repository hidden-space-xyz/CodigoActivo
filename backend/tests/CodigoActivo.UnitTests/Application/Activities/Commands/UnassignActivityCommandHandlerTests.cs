using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class UnassignActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UnassignActivityCommandHandler sut;

    public UnassignActivityCommandHandlerTests()
    {
        sut = new UnassignActivityCommandHandler(
            activities,
            new SignupGate(events, users, clock),
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activity.Id, Guid.NewGuid(), IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(Guid.NewGuid(), Guid.NewGuid(), IsAdmin: true),
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
        var activity = NewActivity();
        var assignment = activity.SignUp(Guid.NewGuid());
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(assignment.ActivityId, assignment.UserId, IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
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
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, PastStart, PastEnd);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activityId, assignment.UserId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(assignment);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncStartedActivityWithOpenWindowReturnsAlreadyStarted()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = ActivityStartsAt.AddMinutes(1);
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, ActivityEndsAt);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activityId, assignment.UserId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityAlreadyStarted);
        activity.Assignments.Should().Contain(assignment);
    }

    [Fact]
    public async Task HandleAsyncWindowOpenForMemberRemovesAssignment()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activityId, assignment.UserId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
