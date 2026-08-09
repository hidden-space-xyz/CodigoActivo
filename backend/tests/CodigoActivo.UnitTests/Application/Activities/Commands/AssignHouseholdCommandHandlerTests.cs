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

public sealed class AssignHouseholdCommandHandlerTests
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
    private readonly AssignHouseholdCommandHandler sut;

    public AssignHouseholdCommandHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new AssignHouseholdCommandHandler(
            activities,
            users,
            new SignupGate(activities, users, executor, clock),
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
            new ListAssignmentStatusTypesQueryHandler(statuses, executor, new FakeHybridCache()),
            executor,
            clock,
            uow,
            cacheInvalidator
        );
    }

    private void NoStatusCatalog()
    {
        statuses.Query().Returns(new List<AssignmentStatusType>().AsQueryable());
    }

    [Fact]
    public async Task HandleAsyncNoAssignmentsReturnsHouseholdAssignmentsRequired()
    {
        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new AssignHouseholdRequest([]),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdAssignmentsRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncWindowClosedForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(
                activityId,
                Guid.NewGuid(),
                new AssignHouseholdRequest([new(Guid.NewGuid(), Guid.NewGuid())]),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncMemberNotInHouseholdReturnsMemberNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers();

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(
                activityId,
                actingUserId,
                new AssignHouseholdRequest([new(strangerId, Guid.NewGuid())]),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdMemberNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRoleUnknownReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            }
        );

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(
                activityId,
                actingUserId,
                new AssignHouseholdRequest([new(actingUserId, Guid.NewGuid())]),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForNonSocioMemberReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
            new(childId, SeedIds.ActivityRoleTypes.Leader),
        ]);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(activityId, actingUserId, request, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await activities
            .DidNotReceiveWithAnyArgs()
            .AddAssignmentAsync(
                new ActivityUserRoleAssignment(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForSocioHouseholdCreatesAssignmentsForAll()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = DuringEarly;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd, EarlyStart);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());
        statuses.RequestedStatusNamed("Solicitado");

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
            new(childId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(activityId, actingUserId, request, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForParticipantHouseholdReturnsSignupEarlyOnly()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = DuringEarly;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd, EarlyStart);
        users.HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Participant,
            },
            ParticipantChild(childId, actingUserId)
        );

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Participant),
            new(childId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(activityId, actingUserId, request, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupEarlyOnly);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncMixedValidRolesCreatesAssignmentsForAllAndInvalidatesCache()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());
        statuses.RequestedStatusNamed("Solicitado");

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
            new(childId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(activityId, actingUserId, request, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null
                    && a.UserId == actingUserId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                ),
                Arg.Any<CancellationToken>()
            );
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null
                    && a.UserId == childId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
                ),
                Arg.Any<CancellationToken>()
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
    public async Task HandleAsyncMixOfNewAndExistingCreatesMissingAndSkipsExisting()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    new() { UserId = childId, ActivityId = activityId },
                }.AsQueryable()
            );
        NoStatusCatalog();

        var request = new AssignHouseholdRequest([
            new(actingUserId, roleId),
            new(actingUserId, roleId),
            new(childId, roleId),
        ]);

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(activityId, actingUserId, request, IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(actingUserId);
        result.Value[0].Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        result.Value[0].Status.Name.Should().BeEmpty();
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a => a != null && a.UserId == actingUserId),
                Arg.Any<CancellationToken>()
            );
        await activities
            .DidNotReceive()
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a => a != null && a.UserId == childId),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSelfAndChildSendsOneEmailListingEveryone()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());
        statuses.RequestedStatusNamed("Solicitado");
        roleTypes.CatalogRoles();

        await sut.HandleAsync(
            new AssignHouseholdCommand(
                activityId,
                actingUserId,
                new AssignHouseholdRequest([
                    new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
                    new(childId, SeedIds.ActivityRoleTypes.Participant),
                ]),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        var message = emailSender.Sent.Should().ContainSingle().Which;
        message.ToAddress.Should().Be("ada@parent.test");
        message
            .TextBody.Should()
            .Contain("Ada Parent (Líder)")
            .And.Contain("Kid One (Participante)");
    }

    [Fact]
    public async Task HandleAsyncEmailDeliveryFailsStillPersistsTheAssignments()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId));
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());
        statuses.RequestedStatusNamed("Solicitado");
        roleTypes.CatalogRoles();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp is down");

        var result = await sut.HandleAsync(
            new AssignHouseholdCommand(
                activityId,
                actingUserId,
                new AssignHouseholdRequest([new(actingUserId, SeedIds.ActivityRoleTypes.Leader)]),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
