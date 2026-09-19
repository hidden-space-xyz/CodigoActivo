using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class AssignActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IAssignmentStatusTypeRepository statuses =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly AssignActivityCommandHandler sut;

    public AssignActivityCommandHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new AssignActivityCommandHandler(
            activities,
            users,
            new SignupGate(activities, users, executor, clock),
            new TermsGate(activities, events, executor, clock),
            new ListAssignmentStatusTypesQueryHandler(statuses, executor, new FakeHybridCache()),
            executor,
            clock,
            uow,
            cacheInvalidator
        );
    }

    private void AssignmentExists(bool exists)
    {
        activities
            .AssignmentExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(exists);
    }

    [Fact]
    public async Task HandleAsyncActivityWindowMissingReturnsNotFound()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new AssignRequest(Guid.NewGuid()),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncOutsideWindowForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, PastStart, PastEnd);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new AssignRequest(Guid.NewGuid()),
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
    public async Task HandleAsyncUserMissingReturnsUserNotFound()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers();

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncVolunteerRoleForNonSocioUserPersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Volunteer),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Volunteer);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null
                    && a.UserId == userId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForSocioUserPersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Member);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null
                    && a.UserId == userId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForNonSocioUserReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForNonSocioUserAsAdminReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, PastStart, PastEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
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
    public async Task HandleAsyncUnknownRoleForSocioUserReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Member);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(Guid.NewGuid()),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAssignmentAlreadyExistsReturnsConflict()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(true);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentAlreadyExists);
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
    public async Task HandleAsyncValidRequestAsAdminPersistsReturnsRequestedStatusAndInvalidatesCache()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;
        activities.HasActivityWindow(events, activityId, PastStart, PastEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(roleId),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.ActivityId.Should().Be(activityId);
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        result.Value.Status.Name.Should().Be("Solicitado");
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    MatchesAssignment(
                        a,
                        userId,
                        activityId,
                        roleId,
                        SeedIds.AssignmentStatusTypes.Requested
                    )
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
    public async Task HandleAsyncMemberAtExactSignupStartIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenStart;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(roleId),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null && a.UserId == userId && a.ActivityId == activityId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncMemberAtExactSignupEndIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenEnd;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(roleId),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a != null && a.UserId == userId && a.ActivityId == activityId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForSocioIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetUser(userId, SeedIds.UserTypes.Member);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForSponsorIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetUser(userId, SeedIds.UserTypes.Sponsor);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForParticipantReturnsSignupEarlyOnly()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetUser(userId, SeedIds.UserTypes.Participant);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupEarlyOnly);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForChildOfSocioIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetChildOf(childId, SeedIds.UserTypes.Member);
        AssignmentExists(false);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                childId,
                childId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForChildOfParticipantReturnsSignupEarlyOnly()
    {
        var activityId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetChildOf(childId, SeedIds.UserTypes.Participant);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                childId,
                childId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupEarlyOnly);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncBeforeEarlySignupWindowForSocioReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        clock.UtcNow = BeforeEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.TargetUser(userId, SeedIds.UserTypes.Member);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
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
    public async Task HandleAsyncNoEarlySignupWindowForSocioReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        clock.UtcNow = DuringEarly;

        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, SeedIds.UserTypes.Member);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
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
    public async Task HandleAsyncTermsRequiredWithoutAcceptFlagReturnsTermsAcceptanceRequired()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: Guid.NewGuid()
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredWithAcceptFlagPersistsAcceptanceAndAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: eventId,
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == eventId
                    && a.UserId == userId
                    && a.TermsDocumentId == termsDocumentId
                    && a.Accepted
                    && a.DecidedAt == Now
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncTermsAlreadyAcceptedPersistsWithoutNewAcceptance()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(termsDocumentId);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredAsAdminWithoutAcceptFlagReturnsTermsAcceptanceRequired()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        activities.HasActivityWindow(
            events,
            activityId,
            PastStart,
            PastEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: Guid.NewGuid()
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredAsAdminWithAcceptFlagPersistsAcceptance()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            PastStart,
            PastEnd,
            eventId: eventId,
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
                ),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == eventId
                    && a.UserId == userId
                    && a.TermsDocumentId == termsDocumentId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredForChildTargetRecordsActingUserAcceptance()
    {
        var activityId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: eventId,
            termsDocumentId: termsDocumentId
        );
        users.HouseholdUsers(ParticipantChild(childId, parentId));
        AssignmentExists(false);
        events.TermsAccepted(null);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                childId,
                parentId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null && a.EventId == eventId && a.UserId == parentId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredForOtherUserAsAdminSkipsTermsGate()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: Guid.NewGuid()
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                Guid.NewGuid(),
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: true
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRequiredDocumentAlreadyRejectedWithoutNewDecisionStillRequiresAcceptance()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.HasTermsDecisions(
            new EventTermsAcceptance
            {
                TermsDocumentId = termsDocumentId,
                Accepted = false,
                DecidedAt = Now.AddDays(-1),
            }
        );

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRejectingRequiredDocumentReturnsTermsAcceptanceRequiredWithoutPersistingRejection()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result
            .Error.Code.Should()
            .Be(
                ErrorCode.EventTermsAcceptanceRequired,
                "rejecting a required document must not be stored as a permanent, unrecoverable decision"
            );
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAcceptingRequiredDocumentAfterEarlierRejectionSucceeds()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: termsDocumentId
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);

        var rejected = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );
        rejected.Error!.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);

        statuses.RequestedStatusNamed("Solicitado");

        var accepted = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        accepted
            .IsSuccess.Should()
            .BeTrue(
                "a mistaken rejection of a required document must not permanently lock the user out of the event"
            );
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null && a.TermsDocumentId == termsDocumentId && a.Accepted
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRejectingOptionalDocumentPersistsRejectionAndDoesNotBlockSignup()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: eventId,
            termsDocumentId: termsDocumentId,
            termsRequired: false
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        events.TermsAccepted(null);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result
            .IsSuccess.Should()
            .BeTrue(
                "an optional document never blocks the signup, regardless of the user's decision"
            );
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == eventId
                    && a.UserId == userId
                    && a.TermsDocumentId == termsDocumentId
                    && !a.Accepted
                    && a.DecidedAt == Now
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncOptionalDocumentWithStoredRejectionAcceptingOverwritesRowInPlace()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: termsDocumentId,
            termsRequired: false
        );
        users.TargetUser(userId, SeedIds.UserTypes.Participant);
        AssignmentExists(false);
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = false,
            DecidedAt = Now.AddDays(-1),
        };
        events.HasTermsDecisions(stored);
        statuses.RequestedStatusNamed("Solicitado");

        var result = await sut.HandleAsync(
            new AssignActivityCommand(
                activityId,
                userId,
                userId,
                new AssignRequest(
                    SeedIds.ActivityRoleTypes.Participant,
                    TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
                ),
                IsAdmin: false
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored
            .Accepted.Should()
            .BeTrue(
                "a rejection is revisable: a later acceptance overwrites the stored row in place"
            );
        stored.DecidedAt.Should().Be(Now);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
