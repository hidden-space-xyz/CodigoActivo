using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
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

public sealed class AssignHouseholdCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly TestClock clock = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AssignHouseholdCommandHandler sut;

    public AssignHouseholdCommandHandlerTests()
    {
        sut = new AssignHouseholdCommandHandler(
            activities,
            users,
            currentUser,
            new SignupGate(events, users, clock),
            new TermsGate(events, termsAcceptances, clock),
            clock,
            uow
        );
    }

    private Task<Result<IReadOnlyList<UserId>>> AssignHouseholdAsync(
        Guid activityId,
        Guid actingUserId,
        AssignHouseholdRequest request,
        bool isAdmin
    )
    {
        currentUser.Id = UserId.From(actingUserId);
        currentUser.IsAdmin = isAdmin;
        return sut.HandleAsync(
            request.ToCommand(ActivityId.From(activityId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncNoAssignmentsReturnsHouseholdAssignmentsRequired()
    {
        var result = await AssignHouseholdAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignHouseholdRequest([]),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityHouseholdAssignmentsRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await AssignHouseholdAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignHouseholdRequest([new(Guid.NewGuid(), Guid.NewGuid())]),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncWindowClosedForMemberReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, PastStart, PastEnd);

        var result = await AssignHouseholdAsync(
            activityId,
            Guid.NewGuid(),
            new AssignHouseholdRequest([new(Guid.NewGuid(), Guid.NewGuid())]),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncMemberNotInHouseholdReturnsMemberNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers();

        var result = await AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(strangerId, Guid.NewGuid())]),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityHouseholdMemberNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRoleUnknownReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(
            Persisted.As<User>(
                new
                {
                    Id = actingUserId,
                    FirstName = "Ada",
                    LastName = "Parent",
                    UserType = UserType.Member,
                }
            )
        );

        var result = await AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(actingUserId, Guid.NewGuid())]),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLeaderRoleForNonSocioMemberReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));

        var request = new AssignHouseholdRequest([
            new(actingUserId, KnownIds.ActivityRoleTypes.Leader),
            new(childId, KnownIds.ActivityRoleTypes.Leader),
        ]);

        var result = await AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleNotAllowed);
        activity.Assignments.Should().BeEmpty();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTermsRequiredWithoutAcceptFlagReturnsTermsAcceptanceRequired()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(
            events,
            activityId,
            OpenStart,
            OpenEnd,
            eventId: Guid.NewGuid(),
            termsDocumentId: Guid.NewGuid()
        );
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        termsAcceptances.TermsAccepted(null);

        var result = await AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(childId, KnownIds.ActivityRoleTypes.Participant)]),
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
    public async Task HandleAsyncTermsRequiredWithAcceptFlagPersistsAcceptanceForActingUser()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
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
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        termsAcceptances.TermsAccepted(null);

        var result = await AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest(
                [new(childId, KnownIds.ActivityRoleTypes.Participant)],
                TermsDecisions: [new TermsDecisionRequest(termsDocumentId, true)]
            ),
            isAdmin: false
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(UserId.From(childId));
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(actingUserId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
                    && a.Accepted
                    && a.DecidedAt == Now
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForSocioHouseholdCreatesAssignmentsForAll()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = DuringEarly;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));

        var request = new AssignHouseholdRequest([
            new(actingUserId, KnownIds.ActivityRoleTypes.Leader),
            new(childId, KnownIds.ActivityRoleTypes.Participant),
        ]);

        var result = await AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(UserId.From(actingUserId), UserId.From(childId));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEarlySignupWindowForParticipantHouseholdReturnsSignupEarlyOnly()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = DuringEarly;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd, EarlyStart);
        users.HouseholdUsers(
            Persisted.As<User>(
                new
                {
                    Id = actingUserId,
                    FirstName = "Ada",
                    LastName = "Parent",
                    UserType = UserType.Participant,
                }
            ),
            ParticipantChild(childId, actingUserId)
        );

        var request = new AssignHouseholdRequest([
            new(actingUserId, KnownIds.ActivityRoleTypes.Participant),
            new(childId, KnownIds.ActivityRoleTypes.Participant),
        ]);

        var result = await AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivitySignupEarlyOnly);
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
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));

        var request = new AssignHouseholdRequest([
            new(actingUserId, KnownIds.ActivityRoleTypes.Leader),
            new(childId, KnownIds.ActivityRoleTypes.Participant),
        ]);

        var result = await AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(UserId.From(actingUserId), UserId.From(childId));
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(actingUserId) && a.Role == ActivityRole.Leader
            );
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(childId) && a.Role == ActivityRole.Participant
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncMixOfNewAndExistingCreatesMissingAndSkipsExisting()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var roleId = KnownIds.ActivityRoleTypes.Participant;
        var activity = activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId), ParticipantChild(childId, actingUserId));
        var existing = activity.SignUp(childId, ActivityRole.Participant);

        var request = new AssignHouseholdRequest([
            new(actingUserId, roleId),
            new(actingUserId, roleId),
            new(childId, roleId),
        ]);

        var result = await AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(UserId.From(actingUserId));
        activity
            .Assignments.Should()
            .ContainSingle(a =>
                a.UserId == UserId.From(actingUserId) && a.Status == AssignmentStatus.Requested
            );
        activity
            .Assignments.Should()
            .ContainSingle(a => a.UserId == UserId.From(childId))
            .Which.Should()
            .BeSameAs(existing);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncConcurrentDuplicateSignupReturnsConflict()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId));
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(Assignment),
                    new InvalidOperationException("duplicate")
                )
            );

        var result = await AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(actingUserId, KnownIds.ActivityRoleTypes.Leader)]),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityAssignmentAlreadyExists);
    }

    [Fact]
    public async Task HandleAsyncUniqueViolationOfAnotherTablePropagates()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        clock.UtcNow = Now;
        activities.HasActivityWindow(events, activityId, OpenStart, OpenEnd);
        users.HouseholdUsers(SocioParent(actingUserId));
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(EventTermsAcceptance),
                    new InvalidOperationException("duplicate")
                )
            );

        var act = () =>
            AssignHouseholdAsync(
                activityId,
                actingUserId,
                new AssignHouseholdRequest([
                    new(actingUserId, KnownIds.ActivityRoleTypes.Participant),
                ]),
                isAdmin: false
            );

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }
}
