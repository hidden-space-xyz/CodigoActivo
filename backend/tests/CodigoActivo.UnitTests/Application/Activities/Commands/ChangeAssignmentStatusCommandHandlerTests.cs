using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class ChangeAssignmentStatusCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IAssignmentStatusTypeRepository statuses =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ChangeAssignmentStatusCommandHandler sut;

    public ChangeAssignmentStatusCommandHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new ChangeAssignmentStatusCommandHandler(
            activities,
            statuses,
            new ActivitySignupNotifier(
                activities,
                users,
                executor,
                clock,
                emailSender,
                new ApplicationOptions { BaseUrl = "https://app.test" },
                new ListActivityRoleTypesQueryHandler(roleTypes, executor, new FakeHybridCache()),
                NullLogger<ActivitySignupNotifier>.Instance
            ),
            uow,
            cacheInvalidator
        );
    }

    private void StatusFound(Guid id, string name)
    {
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new AssignmentStatusType
                {
                    Description = "Descripción de prueba",
                    Id = id,
                    Name = name,
                    Color = "#0f0",
                }
            );
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        activities.ExistingAssignment(null);

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                Guid.NewGuid(),
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
        activities.ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        AssignmentStatusType? missingStatus = null;
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(missingStatus);

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = Assignment(userId, activityId);
        activities.ExistingAssignment(assignment);
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new AssignmentStatusType
                {
                    Description = "Descripción de prueba",
                    Id = statusId,
                    Name = "Confirmado",
                    Color = "#0f0",
                }
            );

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
                userId,
                new ChangeAssignmentStatusRequest(statusId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        assignment.AssignmentStatusId.Should().Be(statusId);
        result.Value.Status.Id.Should().Be(statusId);
        result.Value.Status.Name.Should().Be("Confirmado");
        result.Value.RoleTypeName.Should().BeNull();
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        roleTypes.CatalogRoles();
        activities.ExistingAssignment(
            Assignment(
                userId,
                activityId,
                roleTypeId: SeedIds.ActivityRoleTypes.Volunteer,
                statusId: SeedIds.AssignmentStatusTypes.Requested
            )
        );
        StatusFound(SeedIds.AssignmentStatusTypes.Confirmed, "Confirmado");

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
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
        var activityId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.TargetChildOf(childId, SeedIds.UserTypes.Member);
        roleTypes.CatalogRoles();
        activities.ExistingAssignment(
            Assignment(
                childId,
                activityId,
                roleTypeId: SeedIds.ActivityRoleTypes.Participant,
                statusId: SeedIds.AssignmentStatusTypes.Requested
            )
        );
        StatusFound(SeedIds.AssignmentStatusTypes.Denied, "Denegado");

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        roleTypes.CatalogRoles();
        var assignment = Assignment(
            userId,
            activityId,
            roleTypeId: SeedIds.ActivityRoleTypes.Volunteer,
            statusId: SeedIds.AssignmentStatusTypes.Requested
        );
        activities.ExistingAssignment(assignment);
        StatusFound(SeedIds.AssignmentStatusTypes.Confirmed, "Confirmado");
        emailSender.ThrowOnSend = new InvalidOperationException("smtp is down");

        var result = await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        roleTypes.CatalogRoles();
        activities.ExistingAssignment(
            Assignment(userId, activityId, statusId: SeedIds.AssignmentStatusTypes.Confirmed)
        );
        StatusFound(SeedIds.AssignmentStatusTypes.Confirmed, "Confirmado");

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        roleTypes.CatalogRoles();
        activities.ExistingAssignment(
            Assignment(userId, activityId, statusId: SeedIds.AssignmentStatusTypes.Confirmed)
        );
        StatusFound(SeedIds.AssignmentStatusTypes.Requested, "Solicitado");

        await sut.HandleAsync(
            new ChangeAssignmentStatusCommand(
                activityId,
                userId,
                new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Requested)
            ),
            TestContext.Current.CancellationToken
        );

        emailSender.Sent.Should().BeEmpty();
    }
}
