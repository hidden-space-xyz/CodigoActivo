using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetActivityByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetActivityByIdQueryHandler sut;

    public GetActivityByIdQueryHandlerTests()
    {
        sut = new GetActivityByIdQueryHandler(store, new FakeQueryExecutor());
    }

    private static AssignmentRow RoleAssignment(Guid activityId, Guid roleTypeId, Guid statusId)
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
        var activity = NewActivityRow();
        store.Activities.Add(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(ActivityId.From(activity.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(activity.Id);
        result.Value.ModalityName.Should().Be("Presencial");
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(ActivityId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task HandleAsyncAssignmentsExceedDesiredCountFlagsOnlySaturatedRole()
    {
        var activity = NewActivityRow();
        activity.RoleCapacities.Add(
            CapacityRow(activity.Id, KnownIds.ActivityRoleTypes.Participant, 1)
        );
        activity.RoleCapacities.Add(
            CapacityRow(activity.Id, KnownIds.ActivityRoleTypes.Volunteer, 2)
        );
        activity.Assignments.Add(
            RoleAssignment(
                activity.Id,
                KnownIds.ActivityRoleTypes.Participant,
                KnownIds.AssignmentStatusTypes.Confirmed
            )
        );
        activity.Assignments.Add(
            RoleAssignment(
                activity.Id,
                KnownIds.ActivityRoleTypes.Participant,
                KnownIds.AssignmentStatusTypes.Requested
            )
        );
        store.Activities.Add(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(ActivityId.From(activity.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == KnownIds.ActivityRoleTypes.Participant
            )
            .Should()
            .BeEquivalentTo(
                new ActivityRoleCapacityResponse(KnownIds.ActivityRoleTypes.Participant, 1, true)
            );
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == KnownIds.ActivityRoleTypes.Volunteer
            )
            .IsHighDemand.Should()
            .BeFalse();
    }

    [Fact]
    public async Task HandleAsyncNonDeniedAssignmentsAtDesiredCountRoleNotHighDemand()
    {
        var activity = NewActivityRow();
        activity.RoleCapacities.Add(
            CapacityRow(activity.Id, KnownIds.ActivityRoleTypes.Participant, 1)
        );
        activity.Assignments.Add(
            RoleAssignment(
                activity.Id,
                KnownIds.ActivityRoleTypes.Participant,
                KnownIds.AssignmentStatusTypes.Confirmed
            )
        );
        activity.Assignments.Add(
            RoleAssignment(
                activity.Id,
                KnownIds.ActivityRoleTypes.Participant,
                KnownIds.AssignmentStatusTypes.Denied
            )
        );
        activity.Assignments.Add(
            RoleAssignment(
                activity.Id,
                KnownIds.ActivityRoleTypes.Volunteer,
                KnownIds.AssignmentStatusTypes.Confirmed
            )
        );
        store.Activities.Add(activity);

        var result = await sut.HandleAsync(
            new GetActivityByIdQuery(ActivityId.From(activity.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleCapacities.Should().OnlyContain(c => !c.IsHighDemand);
    }
}
