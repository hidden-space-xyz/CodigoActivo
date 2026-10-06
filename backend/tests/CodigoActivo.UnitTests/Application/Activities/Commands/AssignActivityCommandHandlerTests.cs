using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class AssignActivityCommandHandlerTests
{
    private static readonly Guid SelfId = Guid.NewGuid();

    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly TestClock clock = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AssignActivityCommandHandler sut;

    public AssignActivityCommandHandlerTests()
    {
        sut = new AssignActivityCommandHandler(
            activities,
            users,
            new ActingUserPolicy(currentUser, users),
            currentUser,
            new SignupGate(events, users, clock),
            new TermsGate(events, termsAcceptances, clock),
            clock,
            uow
        );
    }

    private Task<Result> AssignAsync(
        Guid activityId,
        Guid userId,
        Guid actingUserId,
        AssignRequest request,
        bool isAdmin
    )
    {
        currentUser.Id = UserId.From(actingUserId);
        currentUser.IsAdmin = isAdmin;
        return sut.HandleAsync(
            request.ToCommand(ActivityId.From(activityId), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncActivityWindowMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await AssignAsync(
            Guid.NewGuid(),
            SelfId,
            SelfId,
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncOutsideWindowForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, PastStart, PastEnd);

        var result = await AssignAsync(
            activityId,
            SelfId,
            SelfId,
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupClosed);
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

        var result = await AssignAsync(
            activityId,
            SelfId,
            SelfId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncVolunteerRoleForNonSocioUserPersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Volunteer),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(userId) && a.Role == ActivityRole.Volunteer
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForSocioUserPersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Leader),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        activity
            .Assignments.Should()
            .ContainSingle(a => a.UserId == UserId.From(userId) && a.Role == ActivityRole.Leader);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncConcurrentDuplicateSignupReturnsConflict()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Member);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(Assignment),
                    new InvalidOperationException("duplicate")
                )
            );

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Leader),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentAlreadyExists);
    }

    [Fact]
    public async Task HandleAsyncUniqueViolationOfAnotherTablePropagates()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Member);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(EventTermsAcceptance),
                    new InvalidOperationException("duplicate")
                )
            );

        var act = () =>
            AssignAsync(
                activityId,
                userId,
                userId,
                new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
                isAdmin: false
            );

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForNonSocioUserReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Leader),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleNotAllowed);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Leader),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleNotAllowed);
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
        users.TargetUser(userId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAssignmentAlreadyExistsReturnsConflict()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        var existing = activity.SignUp(userId, ActivityRole.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentAlreadyExists);
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(existing);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestAsAdminPersistsRequestedStatusAndInvalidatesCache()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Participant;
        var activity = activities.HasActivityWindow(events, activityId, PastStart, PastEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(roleId),
            isAdmin: true
        );

        result.IsSuccess.Should().BeTrue();
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                MatchesAssignment(
                    a,
                    userId,
                    activityId,
                    CatalogIds.ActivityRoles.ValueOf(roleId),
                    AssignmentStatus.Requested
                )
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncStartedActivityWithOpenWindowReturnsAlreadyStarted()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = ActivityStartsAt;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, ActivityEndsAt);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityAlreadyStarted);
        activity.Assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncMemberAtExactSignupStartIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenStart;

        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(roleId),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(userId) && a.ActivityId == ActivityId.From(activityId)
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncMemberAtExactSignupEndIsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenEnd;

        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(roleId),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(userId) && a.ActivityId == ActivityId.From(activityId)
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
        users.TargetUser(userId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
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
        users.TargetUser(userId, KnownIds.UserTypes.Sponsor);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupEarlyOnly);
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
        users.TargetChildOf(childId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            childId,
            childId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
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
        users.TargetChildOf(childId, KnownIds.UserTypes.Participant);

        var result = await AssignAsync(
            activityId,
            childId,
            childId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupEarlyOnly);
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
        users.TargetUser(userId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupClosed);
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
        users.TargetUser(userId, KnownIds.UserTypes.Member);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupClosed);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(userId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(termsDocumentId);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: true
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(userId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
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
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            childId,
            parentId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(parentId)
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            Guid.NewGuid(),
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: true
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.HasTermsDecisions(StoredDecision(termsDocumentId, false, Now.AddDays(-1)));

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
            ),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result
            .Error.Code.Should()
            .Be(
                ApplicationErrorCode.EventTermsAcceptanceRequired,
                "rejecting a required document must not be stored as a permanent, unrecoverable decision"
            );
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var rejected = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
            ),
            isAdmin: false
        );
        rejected.Error!.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);

        var accepted = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: false
        );

        accepted
            .IsSuccess.Should()
            .BeTrue(
                "a mistaken rejection of a required document must not permanently lock the user out of the event"
            );
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
                    && a.Accepted
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        termsAcceptances.TermsAccepted(null);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, false)]
            ),
            isAdmin: false
        );

        result
            .IsSuccess.Should()
            .BeTrue(
                "an optional document never blocks the signup, regardless of the user's decision"
            );
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(userId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
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
        users.TargetUser(userId, KnownIds.UserTypes.Participant);
        var stored = StoredDecision(termsDocumentId, false, Now.AddDays(-1));
        termsAcceptances.HasTermsDecisions(stored);

        var result = await AssignAsync(
            activityId,
            userId,
            userId,
            new AssignRequest(
                KnownIds.ActivityRoleTypes.Participant,
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        stored
            .Accepted.Should()
            .BeTrue(
                "a rejection is revisable: a later acceptance overwrites the stored row in place"
            );
        stored.DecidedAt.Should().Be(Now);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSomeoneOutsideTheHouseholdReturnsForbidden()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);

        var result = await AssignAsync(
            activityId,
            Guid.NewGuid(),
            SelfId,
            new AssignRequest(KnownIds.ActivityRoleTypes.Participant),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActingForAnotherUserForbidden);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
