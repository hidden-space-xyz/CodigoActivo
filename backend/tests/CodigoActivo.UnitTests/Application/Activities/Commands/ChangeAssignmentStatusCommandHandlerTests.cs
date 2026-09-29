using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication.Templates;
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
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ChangeAssignmentStatusCommandHandler sut;

    public ChangeAssignmentStatusCommandHandlerTests()
    {
        sut = new ChangeAssignmentStatusCommandHandler(
            activities,
            readStore,
            new FakeQueryExecutor(),
            new ActivitySignupNotifier(
                readStore,
                new FakeQueryExecutor(),
                emailSender,
                new SignupEmailComposer(
                    new ApplicationOptions { BaseUrl = "https://app.test" },
                    clock
                ),
                NullLogger<ActivitySignupNotifier>.Instance
            ),
            uow,
            cacheInvalidator
        );
    }

    private void StatusExists(Guid id)
    {
        readStore.AssignmentStatusTypes.Add(
            new AssignmentStatusTypeRow
            {
                Id = id,
                Name = "Estado",
                Description = "d",
                Color = "#000",
            }
        );
    }

    private Activity SignedUpActivity(Guid userId, Guid? roleTypeId = null, Guid? statusId = null)
    {
        var activity = NewActivity();
        activity.SignUp(userId, roleTypeId, statusId);
        activities.Finds(activity);
        readStore.Activities.Add(SignupActivityRow(activity.Id));
        return activity;
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                Guid.NewGuid(),
                new ChangeAssignmentStatusRequest(Guid.NewGuid())
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncStatusMissingReturnsAssignmentStatusTypeNotFound()
    {
        var userId = Guid.NewGuid();
        var activity = SignedUpActivity(userId);

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(Guid.NewGuid())
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AssignmentStatusTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesStatusPersistsAndInvalidatesCache()
    {
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var activity = SignedUpActivity(userId);
        var assignment = activity.AssignmentOf(userId)!;
        StatusExists(statusId);

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(statusId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        assignment.AssignmentStatusId.Should().Be(statusId);
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
    public async Task HandleAsyncConfirmedSendsDecisionEmailToTheUser()
    {
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = SignedUpActivity(
            userId,
            roleTypeId: SeedIds.ActivityRoleTypes.Volunteer,
            statusId: SeedIds.AssignmentStatusTypes.Requested
        );
        readStore.TargetUser(userId, SeedIds.UserTypes.Participant);
        readStore.CatalogRoles();
        StatusExists(SeedIds.AssignmentStatusTypes.Confirmed);

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed)
            ),
            TestContext.Current.CancellationToken
        );

        var message = emailSender.Sent.Should().ContainSingle().Which;
        message.ToAddress.Should().Be("test@user.test");
        message.Subject.Should().Be("Inscripción confirmada: Taller de robótica");
        message
            .TextBody.Should()
            .Contain("tu inscripción")
            .And.Contain("aprobado")
            .And.Contain("Voluntario");
    }

    [Fact]
    public async Task HandleAsyncDeniedSendsDecisionEmailNamingTheDependentMinor()
    {
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = SignedUpActivity(
            childId,
            roleTypeId: SeedIds.ActivityRoleTypes.Participant,
            statusId: SeedIds.AssignmentStatusTypes.Requested
        );
        readStore.TargetChildOf(childId, SeedIds.UserTypes.Member);
        readStore.CatalogRoles();
        StatusExists(SeedIds.AssignmentStatusTypes.Denied);

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                childId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Denied)
            ),
            TestContext.Current.CancellationToken
        );

        var message = emailSender.Sent.Should().ContainSingle().Which;
        message.ToAddress.Should().Be("ada@parent.test");
        message.Subject.Should().Be("Inscripción rechazada: Taller de robótica");
        message.TextBody.Should().Contain("la inscripción de Kid One").And.Contain("rechazado");
    }

    [Fact]
    public async Task HandleAsyncEmailDeliveryFailsStillPersistsTheStatusChange()
    {
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = SignedUpActivity(
            userId,
            roleTypeId: SeedIds.ActivityRoleTypes.Volunteer,
            statusId: SeedIds.AssignmentStatusTypes.Requested
        );
        var assignment = activity.AssignmentOf(userId)!;
        readStore.TargetUser(userId, SeedIds.UserTypes.Participant);
        readStore.CatalogRoles();
        StatusExists(SeedIds.AssignmentStatusTypes.Confirmed);
        emailSender.ThrowOnSend = new InvalidOperationException("smtp is down");

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        assignment.AssignmentStatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSameStatusReappliedDoesNotSendEmail()
    {
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = SignedUpActivity(userId, statusId: SeedIds.AssignmentStatusTypes.Confirmed);
        readStore.TargetUser(userId, SeedIds.UserTypes.Participant);
        readStore.CatalogRoles();
        StatusExists(SeedIds.AssignmentStatusTypes.Confirmed);

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed)
            ),
            TestContext.Current.CancellationToken
        );

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncMovedBackToRequestedDoesNotSendEmail()
    {
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = SignedUpActivity(userId, statusId: SeedIds.AssignmentStatusTypes.Confirmed);
        readStore.TargetUser(userId, SeedIds.UserTypes.Participant);
        readStore.CatalogRoles();
        StatusExists(SeedIds.AssignmentStatusTypes.Requested);

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activity.Id,
                userId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Requested)
            ),
            TestContext.Current.CancellationToken
        );

        emailSender.Sent.Should().BeEmpty();
    }
}
