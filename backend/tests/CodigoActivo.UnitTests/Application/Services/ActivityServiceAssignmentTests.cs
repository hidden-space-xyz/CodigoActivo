using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class ActivityServiceAssignmentTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IAssignmentStatusTypeRepository statuses =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IActivityModalityTypeRepository modalityTypes =
        Substitute.For<IActivityModalityTypeRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ActivityService sut;

    private static readonly DateTimeOffset OpenStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OpenEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PastStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PastEnd = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    public ActivityServiceAssignmentTests()
    {
        sut = new ActivityService(
            activities,
            events,
            files,
            Substitute.For<IFileService>(),
            statuses,
            roleTypes,
            modalityTypes,
            users,
            new FakeQueryExecutor(),
            clock,
            uow
        );
    }

    private void HasActivityWindow(
        Guid activityId,
        DateTimeOffset signupStart,
        DateTimeOffset signupEnd
    ) =>
        activities
            .Query()
            .Returns(
                new List<Activity>
                {
                    new()
                    {
                        Id = activityId,
                        Event = new Event
                        {
                            Title = "e",
                            Subtitle = "s",
                            SignupStartsAt = signupStart,
                            SignupEndsAt = signupEnd,
                        },
                    },
                }.AsQueryable()
            );

    private void TargetUser(Guid userId, Guid userTypeId) =>
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
                new User
                {
                    Id = userId,
                    FirstName = "Test",
                    LastName = "User",
                    UserTypeId = userTypeId,
                }
            );

    private void HouseholdUsers(params User[] members) =>
        users.Query().Returns(members.AsQueryable());

    private void CatalogRoles() =>
        roleTypes
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ActivityRoleType
                {
                    Id = SeedIds.ActivityRoleTypes.Leader,
                    Name = "Líder",
                    Description = "d",
                },
                new ActivityRoleType
                {
                    Id = SeedIds.ActivityRoleTypes.Volunteer,
                    Name = "Voluntario",
                    Description = "d",
                },
                new ActivityRoleType
                {
                    Id = SeedIds.ActivityRoleTypes.Participant,
                    Name = "Participante",
                    Description = "d",
                },
            ]);

    private void ExistingAssignment(ActivityUserRoleAssignment? assignment) =>
        activities
            .GetAssignmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(assignment);

    private void RequestedStatusNamed(string name) =>
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new AssignmentStatusType
                {
                    Id = SeedIds.AssignmentStatusTypes.Requested,
                    Name = name,
                    Color = "#000",
                }
            );

    private static ActivityUserRoleAssignment Assignment(
        Guid userId,
        Guid activityId,
        ActivityRoleType? role = null,
        AssignmentStatusType? status = null
    ) =>
        new()
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = role!,
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = status!,
        };

    [Fact]
    public async Task AssignAsync_ActivityWindowMissing_ReturnsNotFound()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.AssignAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_OutsideWindowForMember_ReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_UserMissing_ReturnsUserNotFound()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_VolunteerRoleForNonSocioUser_PersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(SeedIds.ActivityRoleTypes.Volunteer),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Volunteer);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == userId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_LeaderRoleForSocioUser_PersistsAssignment()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Member);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == userId && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_LeaderRoleForNonSocioUser_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_LeaderRoleForNonSocioUserAsAdmin_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, PastStart, PastEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(SeedIds.ActivityRoleTypes.Leader),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_UnknownRoleForSocioUser_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Member);

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_AssignmentAlreadyExists_ReturnsConflict()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);
        ExistingAssignment(Assignment(userId, activityId));

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentAlreadyExists);
        await activities
            .DidNotReceiveWithAnyArgs()
            .AddAssignmentAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignAsync_ValidRequestAsAdmin_PersistsAndReturnsRequestedStatus()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;
        HasActivityWindow(activityId, PastStart, PastEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(roleId),
            isAdmin: true,
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
                    a.UserId == userId
                    && a.ActivityId == activityId
                    && a.ActivityRoleTypeId == roleId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_MemberAtExactSignupStart_IsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenStart;

        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(roleId),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == userId && a.ActivityId == activityId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_MemberAtExactSignupEnd_IsOpenAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;

        clock.UtcNow = OpenEnd;

        HasActivityWindow(activityId, OpenStart, OpenEnd);
        TargetUser(userId, SeedIds.UserTypes.Participant);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(roleId),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == userId && a.ActivityId == activityId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignHouseholdAsync_NoAssignments_ReturnsHouseholdAssignmentsRequired()
    {
        var result = await sut.AssignHouseholdAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignHouseholdRequest([]),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdAssignmentsRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignHouseholdAsync_WindowClosedForMember_ReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.AssignHouseholdAsync(
            activityId,
            Guid.NewGuid(),
            new AssignHouseholdRequest([new(Guid.NewGuid(), Guid.NewGuid())]),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignHouseholdAsync_MemberNotInHousehold_ReturnsMemberNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        HouseholdUsers();

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(strangerId, Guid.NewGuid())]),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdMemberNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignHouseholdAsync_RoleUnknown_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            }
        );

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest([new(actingUserId, Guid.NewGuid())]),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignHouseholdAsync_LeaderRoleForNonSocioMember_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            },
            new User
            {
                Id = childId,
                FirstName = "Kid",
                LastName = "One",
                ParentId = actingUserId,
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
            new(childId, SeedIds.ActivityRoleTypes.Leader),
        ]);

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            request,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await activities
            .DidNotReceiveWithAnyArgs()
            .AddAssignmentAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AssignHouseholdAsync_MixedValidRoles_CreatesAssignmentsForAll()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            },
            new User
            {
                Id = childId,
                FirstName = "Kid",
                LastName = "One",
                ParentId = actingUserId,
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());
        RequestedStatusNamed("Solicitado");

        var request = new AssignHouseholdRequest([
            new(actingUserId, SeedIds.ActivityRoleTypes.Leader),
            new(childId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            request,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == actingUserId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                ),
                Arg.Any<CancellationToken>()
            );
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == childId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignHouseholdAsync_MixOfNewAndExisting_CreatesMissingAndSkipsExisting()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Participant;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            },
            new User
            {
                Id = childId,
                FirstName = "Kid",
                LastName = "One",
                ParentId = actingUserId,
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    new() { UserId = childId, ActivityId = activityId },
                }.AsQueryable()
            );

        var request = new AssignHouseholdRequest([
            new(actingUserId, roleId),
            new(actingUserId, roleId),
            new(childId, roleId),
        ]);

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            request,
            isAdmin: true,
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
                Arg.Is<ActivityUserRoleAssignment>(a => a.UserId == actingUserId),
                Arg.Any<CancellationToken>()
            );
        await activities
            .DidNotReceive()
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a => a.UserId == childId),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignAsync_AssignmentMissing_ReturnsNotFound()
    {
        ExistingAssignment(null);

        var result = await sut.UnassignAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnassignAsync_AsAdmin_RemovesWithoutWindowCheck()
    {
        var assignment = Assignment(Guid.NewGuid(), Guid.NewGuid());
        ExistingAssignment(assignment);

        var result = await sut.UnassignAsync(
            assignment.ActivityId,
            assignment.UserId,
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignAsync_WindowClosedForMember_ReturnsSignupClosed()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        clock.UtcNow = Now;
        ExistingAssignment(assignment);
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.UnassignAsync(
            activityId,
            assignment.UserId,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        activities.DidNotReceiveWithAnyArgs().RemoveAssignment(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnassignAsync_WindowOpenForMember_RemovesAssignment()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        clock.UtcNow = Now;
        ExistingAssignment(assignment);
        HasActivityWindow(activityId, OpenStart, OpenEnd);

        var result = await sut.UnassignAsync(
            activityId,
            assignment.UserId,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_AssignmentMissing_ReturnsNotFound()
    {
        ExistingAssignment(null);

        var result = await sut.ChangeStatusAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentStatusRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeStatusAsync_StatusMissing_ReturnsAssignmentStatusTypeNotFound()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((AssignmentStatusType?)null);

        var result = await sut.ChangeStatusAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentStatusRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AssignmentStatusTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeStatusAsync_ValidRequest_UpdatesStatusAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = Assignment(userId, activityId);
        ExistingAssignment(assignment);
        statuses
            .FindAsync(
                Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new AssignmentStatusType
                {
                    Id = statusId,
                    Name = "Confirmado",
                    Color = "#0f0",
                }
            );

        var result = await sut.ChangeStatusAsync(
            activityId,
            userId,
            new ChangeAssignmentStatusRequest(statusId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        assignment.AssignmentStatusId.Should().Be(statusId);
        result.Value.Status.Id.Should().Be(statusId);
        result.Value.Status.Name.Should().Be("Confirmado");
        result.Value.RoleTypeName.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRoleAsync_AssignmentMissing_ReturnsNotFound()
    {
        ExistingAssignment(null);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeRoleAsync_RoleTypeMissing_ReturnsRoleTypeNotFound()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((ActivityRoleType?)null);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeRoleAsync_ValidRequest_UpdatesRoleAndPersists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Leader;
        var assignment = Assignment(userId, activityId);
        ExistingAssignment(assignment);
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ActivityRoleType
                {
                    Id = roleId,
                    Name = "Líder",
                    Description = "d",
                }
            );

        var result = await sut.ChangeRoleAsync(
            activityId,
            userId,
            new ChangeAssignmentRoleRequest(roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    a.UserId == userId
                    && a.ActivityId == activityId
                    && a.ActivityRoleTypeId == roleId
                    && a.AssignmentStatusId == assignment.AssignmentStatusId
                ),
                Arg.Any<CancellationToken>()
            );
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.RoleTypeName.Should().Be("Líder");
        result.Value.Status.Name.Should().BeEmpty();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRoleAsync_SameRoleAsCurrent_ReturnsUnchangedWithoutRemovingOrSaving()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType { Name = "Solicitado", Color = "#000" },
        };
        ExistingAssignment(assignment);
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ActivityRoleType
                {
                    Id = roleId,
                    Name = "Líder",
                    Description = "d",
                }
            );

        var result = await sut.ChangeRoleAsync(
            activityId,
            userId,
            new ChangeAssignmentRoleRequest(roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.RoleTypeName.Should().Be("Líder");
        result.Value.Status.Id.Should().Be(statusId);
        result.Value.Status.Name.Should().Be("Solicitado");
        activities.DidNotReceiveWithAnyArgs().RemoveAssignment(default!);
        await activities
            .DidNotReceiveWithAnyArgs()
            .AddAssignmentAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_ActivityMissing_ReturnsNotFound()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.VerifyTimeOverlapsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_OverlappingAssignments_ReportsOverlapsExcludingTargetAndOtherUsers()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        var clash = OverlapActivity(Guid.NewGuid(), 11, 13, "Choque");
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        HasAssignments(
            OverlapAssignment(userId, target),
            OverlapAssignment(userId, clash),
            OverlapAssignment(Guid.NewGuid(), OverlapActivity(Guid.NewGuid(), 11, 13, "Ajeno"))
        );

        var result = await sut.VerifyTimeOverlapsAsync(
            target.Id,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeTrue();
        result.Value.Overlaps.Should().ContainSingle();
        result.Value.Overlaps[0].ActivityId.Should().Be(clash.Id);
        result.Value.Overlaps[0].Title.Should().Be("Choque");
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_MultipleOverlaps_OrdersByStartThenActivityId()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 9, 14);
        var earliest = OverlapActivity(Guid.NewGuid(), 10, 11);
        var tieFirst = OverlapActivity(new Guid("00000000-0000-0000-0000-000000000001"), 11, 12);
        var tieSecond = OverlapActivity(new Guid("00000000-0000-0000-0000-000000000002"), 11, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        HasAssignments(
            OverlapAssignment(userId, tieSecond),
            OverlapAssignment(userId, tieFirst),
            OverlapAssignment(userId, earliest)
        );

        var result = await sut.VerifyTimeOverlapsAsync(
            target.Id,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Overlaps.Select(o => o.ActivityId)
            .Should()
            .Equal(earliest.Id, tieFirst.Id, tieSecond.Id);
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_DisjointAssignments_ReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        HasAssignments(OverlapAssignment(userId, OverlapActivity(Guid.NewGuid(), 13, 14)));

        var result = await sut.VerifyTimeOverlapsAsync(
            target.Id,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_AdjacentAssignments_ReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        HasAssignments(OverlapAssignment(userId, OverlapActivity(Guid.NewGuid(), 12, 14)));

        var result = await sut.VerifyTimeOverlapsAsync(
            target.Id,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    private void HasAssignments(params ActivityUserRoleAssignment[] assignments) =>
        activities.QueryAssignments().Returns(assignments.AsQueryable());

    private static Activity OverlapActivity(
        Guid id,
        int startHour,
        int endHour,
        string title = "Act"
    ) =>
        new()
        {
            Id = id,
            Title = title,
            Description = "{}",
            Location = "l",
            ActivityStartsAt = new DateTimeOffset(2026, 7, 10, startHour, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = new DateTimeOffset(2026, 7, 10, endHour, 0, 0, TimeSpan.Zero),
        };

    private static ActivityUserRoleAssignment OverlapAssignment(Guid userId, Activity activity) =>
        new()
        {
            UserId = userId,
            ActivityId = activity.Id,
            Activity = activity,
        };

    private static User HouseholdUser(
        Guid id,
        string firstName,
        string lastName,
        Guid? parentId = null
    ) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            ParentId = parentId,
        };

    private static ActivityUserRoleAssignment HouseholdAssignment(
        User user,
        Guid eventId,
        int startHour = 10,
        string roleName = "Participante",
        string statusName = "Solicitado"
    )
    {
        var activity = OverlapActivity(Guid.NewGuid(), startHour, startHour + 1);
        activity.EventId = eventId;
        return new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType { Name = roleName, Description = "d" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType { Name = statusName, Color = "#000" },
        };
    }

    [Fact]
    public async Task GetHouseholdAssignmentsAsync_ParentAndChildAssigned_OrdersByFirstNameAndIncludesChild()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var child = HouseholdUser(Guid.NewGuid(), "Ana", "Kid", actingUserId);
        HasAssignments(
            HouseholdAssignment(parent, eventId, roleName: "Líder", statusName: "Confirmado"),
            HouseholdAssignment(child, eventId)
        );

        var result = await sut.GetHouseholdAssignmentsAsync(
            actingUserId,
            eventId,
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(child.Id);
        result[0].FirstName.Should().Be("Ana");
        result[0].LastName.Should().Be("Kid");
        result[0].RoleName.Should().Be("Participante");
        result[0].StatusName.Should().Be("Solicitado");
        result[1].UserId.Should().Be(actingUserId);
        result[1].FirstName.Should().Be("Zoe");
        result[1].RoleName.Should().Be("Líder");
        result[1].StatusName.Should().Be("Confirmado");
    }

    [Fact]
    public async Task GetHouseholdAssignmentsAsync_SameUserMultipleActivities_OrdersByActivityStart()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var late = HouseholdAssignment(parent, eventId, startHour: 15);
        var early = HouseholdAssignment(parent, eventId, startHour: 9);
        HasAssignments(late, early);

        var result = await sut.GetHouseholdAssignmentsAsync(
            actingUserId,
            eventId,
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.ActivityId).Should().Equal(early.ActivityId, late.ActivityId);
    }

    [Fact]
    public async Task GetHouseholdAssignmentsAsync_StrangerOrOtherEventAssignments_AreExcluded()
    {
        var actingUserId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var parent = HouseholdUser(actingUserId, "Zoe", "Parent");
        var stranger = HouseholdUser(Guid.NewGuid(), "Bob", "Stranger");
        var mine = HouseholdAssignment(parent, eventId);
        HasAssignments(
            mine,
            HouseholdAssignment(parent, Guid.NewGuid()),
            HouseholdAssignment(stranger, eventId)
        );

        var result = await sut.GetHouseholdAssignmentsAsync(
            actingUserId,
            eventId,
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0].ActivityId.Should().Be(mine.ActivityId);
    }

    [Fact]
    public async Task GetHouseholdSignupRolesAsync_SocioParentWithParticipantChild_ReturnsRolesPerMember()
    {
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Ada",
                LastName = "Parent",
                UserTypeId = SeedIds.UserTypes.Member,
            },
            new User
            {
                Id = childId,
                FirstName = "Kid",
                LastName = "One",
                ParentId = actingUserId,
                UserTypeId = SeedIds.UserTypes.Participant,
            },
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Stranger",
                LastName = "Socio",
                UserTypeId = SeedIds.UserTypes.Member,
            }
        );
        CatalogRoles();

        var result = await sut.GetHouseholdSignupRolesAsync(
            actingUserId,
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        var parent = result.Single(m => m.UserId == actingUserId);
        parent
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Leader, "Líder")
            );
        var child = result.Single(m => m.UserId == childId);
        child
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }

    [Fact]
    public async Task GetHouseholdSignupRolesAsync_ParticipantTypeUserWithoutChildren_ReturnsParticipantAndVolunteerOnly()
    {
        var actingUserId = Guid.NewGuid();
        HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Solo",
                LastName = "User",
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );
        CatalogRoles();

        var result = await sut.GetHouseholdSignupRolesAsync(
            actingUserId,
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0]
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }
}
