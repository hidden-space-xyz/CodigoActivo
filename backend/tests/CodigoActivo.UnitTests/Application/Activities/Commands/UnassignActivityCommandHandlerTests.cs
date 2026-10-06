using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
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
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UnassignActivityCommandHandler sut;

    public UnassignActivityCommandHandlerTests()
    {
        sut = new UnassignActivityCommandHandler(
            activities,
            new ActingUserPolicy(currentUser, users),
            currentUser,
            new SignupGate(events, users, clock)
        );
    }

    private Task<Result> UnassignAsync(Guid activityId, Guid userId, bool isAdmin)
    {
        currentUser.Id = UserId.From(userId);
        currentUser.IsAdmin = isAdmin;
        return sut.HandleAsync(
            new UnassignActivityCommand(ActivityId.From(activityId), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await UnassignAsync(activity.Id.Value, Guid.NewGuid(), isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await UnassignAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: true);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public async Task HandleAsyncAsAdminRemovesWithoutWindowCheck()
    {
        var activity = NewActivity();
        var assignment = activity.SignUp(Guid.NewGuid());
        activities.Finds(activity);

        var result = await UnassignAsync(
            assignment.ActivityId.Value,
            assignment.UserId.Value,
            isAdmin: true
        );

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
    }

    [Fact]
    public async Task HandleAsyncWindowClosedForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, PastStart, PastEnd);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await UnassignAsync(activityId, assignment.UserId.Value, isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupClosed);
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(assignment);
    }

    [Fact]
    public async Task HandleAsyncStartedActivityWithOpenWindowReturnsAlreadyStarted()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = ActivityStartsAt.AddMinutes(1);
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, ActivityEndsAt);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await UnassignAsync(activityId, assignment.UserId.Value, isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityAlreadyStarted);
        activity.Assignments.Should().Contain(assignment);
    }

    [Fact]
    public async Task HandleAsyncWindowOpenForMemberRemovesAssignment()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        var assignment = activity.SignUp(Guid.NewGuid());

        var result = await UnassignAsync(activityId, assignment.UserId.Value, isAdmin: false);

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
        activity
            .PullDomainEvents()
            .Should()
            .Equal(new AssignmentWithdrawn(activity.Id, assignment.UserId));
    }

    [Fact]
    public async Task HandleAsyncSomeoneElsesAssignmentReturnsForbidden()
    {
        var activity = NewActivity();
        var assignment = activity.SignUp(Guid.NewGuid());
        activities.Finds(activity);
        currentUser.Id = UserId.New();
        currentUser.IsAdmin = false;

        var result = await sut.HandleAsync(
            new UnassignActivityCommand(activity.Id, assignment.UserId),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActingForAnotherUserForbidden);
        activity.Assignments.Should().Contain(assignment);
    }
}
