using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class ChangeAssignmentRoleCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly ChangeAssignmentRoleCommandHandler sut;

    public ChangeAssignmentRoleCommandHandlerTests()
    {
        sut = new ChangeAssignmentRoleCommandHandler(activities);
    }

    private Task<Result> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentRoleRequest request
    )
    {
        return sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                ActivityId.From(activityId),
                UserId.From(userId),
                request.ActivityRoleTypeId
            ),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await ChangeRoleAsync(
            activity.Id.Value,
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public async Task HandleAsyncRoleTypeMissingReturnsRoleTypeNotFound()
    {
        var userId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var activity = NewActivity();
        activity.SignUp(userId);
        activities.Finds(activity);

        var result = await ChangeRoleAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentRoleRequest(roleTypeId)
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesRoleAndRaisesTheChange()
    {
        var userId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Leader;
        var activity = NewActivity();
        var assignment = activity.SignUp(userId);
        activities.Finds(activity);

        var result = await ChangeRoleAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentRoleRequest(roleId)
        );

        result.IsSuccess.Should().BeTrue();
        activity.Assignments.Should().NotContain(assignment);
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                MatchesAssignment(
                    a,
                    userId,
                    activity.Id.Value,
                    ActivityRole.Leader,
                    assignment.Status
                )
            );
        activity
            .PullDomainEvents()
            .Should()
            .Equal(
                new AssignmentRoleChanged(activity.Id, UserId.From(userId), ActivityRole.Leader)
            );
    }

    [Fact]
    public async Task HandleAsyncSameRoleAsCurrentKeepsAssignmentWithoutRaisingAChange()
    {
        var userId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Volunteer;
        var activity = NewActivity();
        var assignment = activity.SignUp(
            userId,
            ActivityRole.Volunteer,
            AssignmentStatus.Confirmed
        );
        activities.Finds(activity);

        var result = await ChangeRoleAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentRoleRequest(roleId)
        );

        result.IsSuccess.Should().BeTrue();
        assignment.Role.Should().Be(ActivityRole.Volunteer);
        assignment.Status.Should().Be(AssignmentStatus.Confirmed);
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(assignment);
        activity.PullDomainEvents().Should().BeEmpty();
    }
}
