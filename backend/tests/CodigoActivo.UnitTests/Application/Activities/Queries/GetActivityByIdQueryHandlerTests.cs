using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetActivityByIdQueryHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly GetActivityByIdQueryHandler sut;

    public GetActivityByIdQueryHandlerTests()
    {
        sut = new GetActivityByIdQueryHandler(
            activities,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    private static ActivityUserRoleAssignment RoleAssignment(
        Guid activityId,
        Guid roleTypeId,
        Guid statusId
    )
    {
        return new()
        {
            UserId = Guid.NewGuid(),
            ActivityId = activityId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task HandleAsyncActivityExistsReturnsActivity()
    {
        var activity = NewActivity();
        activities.HasActivities(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(activity.Id);
        result.Value.ModalityName.Should().Be("Presencial");
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.HasActivities();

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task HandleAsyncAssignmentsExceedDesiredCountFlagsOnlySaturatedRole()
    {
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            Capacity(activity.Id, SeedIds.ActivityRoleTypes.Participant, 1),
            Capacity(activity.Id, SeedIds.ActivityRoleTypes.Volunteer, 2),
        ];
        activity.Assignments =
        [
            RoleAssignment(
                activity.Id,
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
            RoleAssignment(
                activity.Id,
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.AssignmentStatusTypes.Requested
            ),
        ];
        activities.HasActivities(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .Should()
            .BeEquivalentTo(
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 1, true)
            );
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer
            )
            .IsHighDemand.Should()
            .BeFalse();
    }

    [Fact]
    public async Task HandleAsyncNonDeniedAssignmentsAtDesiredCountRoleNotHighDemand()
    {
        var activity = NewActivity();
        activity.RoleCapacities = [Capacity(activity.Id, SeedIds.ActivityRoleTypes.Participant, 1)];
        activity.Assignments =
        [
            RoleAssignment(
                activity.Id,
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
            RoleAssignment(
                activity.Id,
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.AssignmentStatusTypes.Denied
            ),
            RoleAssignment(
                activity.Id,
                SeedIds.ActivityRoleTypes.Volunteer,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
        ];
        activities.HasActivities(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleCapacities.Should().OnlyContain(c => !c.IsHighDemand);
    }
}
