using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class ChangeAssignmentStatusCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeSignupEmailComposer composer = new();
    private readonly ChangeAssignmentStatusCommandHandler sut;
    private readonly AssignmentDecisionNotification notification;

    public ChangeAssignmentStatusCommandHandlerTests()
    {
        sut = new ChangeAssignmentStatusCommandHandler(activities);
        notification = new AssignmentDecisionNotification(
            new ActivitySignupNotifier(
                readStore,
                new FakeQueryExecutor(),
                emailSender,
                composer,
                NullLogger<ActivitySignupNotifier>.Instance
            )
        );
    }

    private SignupActivity ExpectedActivity()
    {
        var row = readStore.Activities.Should().ContainSingle().Subject;
        return new SignupActivity(
            row.Title,
            row.Event.Title,
            row.EventId,
            row.Location,
            row.ActivityStartsAt,
            row.ActivityEndsAt
        );
    }

    private async Task<Result> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentStatusRequest request
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var activity = await activities.GetByIdAsync(ActivityId.From(activityId), ct);
        var result = await sut.HandleAsync(
            request.ToCommand(ActivityId.From(activityId), UserId.From(userId)),
            ct
        );
        foreach (var changed in DomainEvents.Raised<AssignmentStatusChanged>(activity!))
        {
            await notification.HandleAsync(changed, ct);
        }

        return result;
    }

    private Activity SignedUpActivity(
        Guid userId,
        ActivityRole role = ActivityRole.Participant,
        AssignmentStatus? status = null
    )
    {
        var activity = NewActivity();
        activity.SignUp(userId, role, status);
        activities.Finds(activity);
        readStore.Activities.Add(SignupActivityRow(activity.Id.Value));
        return activity;
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsAssignmentNotFound()
    {
        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                ActivityId.New(),
                UserId.New(),
                AssignmentStatus.Confirmed
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, DomainErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await ChangeStatusAsync(
            activity.Id.Value,
            Guid.NewGuid(),
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesStatus()
    {
        var userId = Guid.NewGuid();
        var statusId = KnownIds.AssignmentStatusTypes.Requested;
        var activity = SignedUpActivity(userId, status: AssignmentStatus.Confirmed);
        var assignment = activity.AssignmentOf(UserId.From(userId))!;

        var result = await ChangeStatusAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentStatusRequest(statusId)
        );

        result.IsSuccess.Should().BeTrue();
        assignment.Status.Should().Be(AssignmentStatus.Requested);
    }

    [Fact]
    public async Task HandleAsyncConfirmedSendsDecisionEmailToTheUser()
    {
        var userId = Guid.NewGuid();
        var activity = SignedUpActivity(
            userId,
            role: ActivityRole.Volunteer,
            status: AssignmentStatus.Requested
        );
        readStore.TargetUser(userId, KnownIds.UserTypes.Participant);
        readStore.CatalogRoles();

        await ChangeStatusAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
        );

        composer
            .Composed.Should()
            .Equal(
                new ComposedSignupEmail(
                    nameof(ISignupEmailComposer.Confirmed),
                    new EmailRecipient("test@user.test", "Test"),
                    null,
                    "Voluntario",
                    ExpectedActivity()
                )
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("test@user.test");
    }

    [Fact]
    public async Task HandleAsyncDeniedSendsDecisionEmailNamingTheDependentMinor()
    {
        var childId = Guid.NewGuid();
        var activity = SignedUpActivity(
            childId,
            role: ActivityRole.Participant,
            status: AssignmentStatus.Requested
        );
        readStore.TargetChildOf(childId, KnownIds.UserTypes.Member);
        readStore.CatalogRoles();

        await ChangeStatusAsync(
            activity.Id.Value,
            childId,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Denied)
        );

        composer
            .Composed.Should()
            .Equal(
                new ComposedSignupEmail(
                    nameof(ISignupEmailComposer.Denied),
                    new EmailRecipient("ada@parent.test", "Ada"),
                    "Kid One",
                    null,
                    ExpectedActivity()
                )
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ada@parent.test");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsyncAddressPendingVerificationGetsNoDecisionEmail(bool minor)
    {
        var pending = new UserRow
        {
            Id = Guid.NewGuid(),
            FirstName = "Pedro",
            LastName = "Pendiente",
            Email = "pedro@pending.test",
            UserStatusTypeId = KnownIds.UserStatusTypes.Pending,
            UserTypeId = KnownIds.UserTypes.Participant,
        };
        var child = new UserRow
        {
            Id = Guid.NewGuid(),
            FirstName = "Kid",
            LastName = "One",
            ParentId = pending.Id,
            Parent = pending,
            UserStatusTypeId = KnownIds.UserStatusTypes.Dependent,
            UserTypeId = KnownIds.UserTypes.Participant,
        };
        readStore.Users.AddRange([pending, child]);
        readStore.CatalogRoles();
        var signedUp = minor ? child.Id : pending.Id;
        var activity = SignedUpActivity(signedUp, status: AssignmentStatus.Requested);

        await ChangeStatusAsync(
            activity.Id.Value,
            signedUp,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
        );

        composer.Composed.Should().BeEmpty();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEmailDeliveryFailsStillStagesTheStatusChange()
    {
        var userId = Guid.NewGuid();
        var activity = SignedUpActivity(
            userId,
            role: ActivityRole.Volunteer,
            status: AssignmentStatus.Requested
        );
        var assignment = activity.AssignmentOf(UserId.From(userId))!;
        readStore.TargetUser(userId, KnownIds.UserTypes.Participant);
        readStore.CatalogRoles();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp is down");

        var result = await ChangeStatusAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
        );

        result.IsSuccess.Should().BeTrue();
        assignment.Status.Should().Be(AssignmentStatus.Confirmed);
    }

    [Fact]
    public async Task HandleAsyncSameStatusReappliedDoesNotSendEmail()
    {
        var userId = Guid.NewGuid();
        var activity = SignedUpActivity(userId, status: AssignmentStatus.Confirmed);
        readStore.TargetUser(userId, KnownIds.UserTypes.Participant);
        readStore.CatalogRoles();

        await ChangeStatusAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Confirmed)
        );

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncMovedBackToRequestedDoesNotSendEmail()
    {
        var userId = Guid.NewGuid();
        var activity = SignedUpActivity(userId, status: AssignmentStatus.Confirmed);
        readStore.TargetUser(userId, KnownIds.UserTypes.Participant);
        readStore.CatalogRoles();

        await ChangeStatusAsync(
            activity.Id.Value,
            userId,
            new ChangeAssignmentStatusRequest(KnownIds.AssignmentStatusTypes.Requested)
        );

        emailSender.Sent.Should().BeEmpty();
    }
}
