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
    private readonly IAssignmentStatusTypeRepository statuses = Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes = Substitute.For<IActivityRoleTypeRepository>();
    private readonly IActivityModalityTypeRepository modalityTypes = Substitute.For<IActivityModalityTypeRepository>();
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
        DateTimeOffset signupEnd,
        params Guid[] allowedRoleIds
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
                        AllowedRoleTypes = allowedRoleIds
                            .Select(r => new ActivityAllowedRoleType { ActivityRoleTypeId = r })
                            .ToList(),
                    },
                }.AsQueryable()
            );

    private void AllowedRoleExists(bool exists) =>
        activities
            .AllowedRoleExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(exists);

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
    public async Task AssignAsync_RoleNotInActivity_ReturnsRoleNotAllowed()
    {
        var activityId = Guid.NewGuid();
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(false);

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
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
        clock.UtcNow = Now;
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(true);
        ExistingAssignment(Assignment(Guid.NewGuid(), activityId));

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
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
        var roleId = Guid.NewGuid();
        HasActivityWindow(activityId, PastStart, PastEnd);
        AllowedRoleExists(true);
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
        var roleId = Guid.NewGuid();

        clock.UtcNow = OpenStart;

        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(true);
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
        var roleId = Guid.NewGuid();

        clock.UtcNow = OpenEnd;

        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(true);
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
        HasActivityWindow(activityId, OpenStart, OpenEnd, Guid.NewGuid());
        users.Query().Returns(new List<User>().AsQueryable());

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
        HasActivityWindow(activityId, OpenStart, OpenEnd, Guid.NewGuid());

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
    public async Task AssignHouseholdAsync_MixOfNewAndExisting_CreatesMissingAndSkipsExisting()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd, roleId);
        users
            .Query()
            .Returns(
                new List<User>
                {
                    new()
                    {
                        Id = childId,
                        FirstName = "Kid",
                        LastName = "One",
                        ParentId = actingUserId,
                    },
                }.AsQueryable()
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
    public async Task ChangeRoleAsync_RoleNotInActivity_ReturnsRoleNotAllowed()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        AllowedRoleExists(false);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeRoleAsync_RoleTypeMissing_ReturnsRoleTypeNotFound()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        AllowedRoleExists(true);
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
        var roleId = Guid.NewGuid();
        var assignment = Assignment(userId, activityId);
        ExistingAssignment(assignment);
        AllowedRoleExists(true);
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
        AllowedRoleExists(true);
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
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.VerifyTimeOverlapsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_OverlappingAssignments_ReportsOverlapsExcludingTarget()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var target = OverlapActivity(activityId, 10, 12);
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        var overlappingId = Guid.NewGuid();
        activities
            .GetUserAssignmentsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([
                new() { ActivityId = activityId, Activity = OverlapActivity(activityId, 10, 12) },
                new()
                {
                    ActivityId = overlappingId,
                    Activity = OverlapActivity(overlappingId, 11, 13, "Choque"),
                },
            ]);

        var result = await sut.VerifyTimeOverlapsAsync(
            activityId,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeTrue();
        result.Value.Overlaps.Should().ContainSingle();
        result.Value.Overlaps[0].ActivityId.Should().Be(overlappingId);
        result.Value.Overlaps[0].Title.Should().Be("Choque");
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_DisjointAssignments_ReportsNoOverlaps()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(OverlapActivity(activityId, 10, 12));
        activities
            .GetUserAssignmentsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([
                new()
                {
                    ActivityId = Guid.NewGuid(),
                    Activity = OverlapActivity(Guid.NewGuid(), 13, 14),
                },
            ]);

        var result = await sut.VerifyTimeOverlapsAsync(
            activityId,
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
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(OverlapActivity(activityId, 10, 12));
        activities
            .GetUserAssignmentsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([
                new()
                {
                    ActivityId = Guid.NewGuid(),
                    Activity = OverlapActivity(Guid.NewGuid(), 12, 14),
                },
            ]);

        var result = await sut.VerifyTimeOverlapsAsync(
            activityId,
            userId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

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

    [Fact]
    public async Task GetHouseholdAssignmentsAsync_NullNavigationsPresent_MapsChildrenAndFallsBack()
    {
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        users
            .ListChildrenWithDetailsAsync(actingUserId, Arg.Any<CancellationToken>())
            .Returns([
                new()
                {
                    Id = childId,
                    FirstName = "Kid",
                    LastName = "One",
                },
            ]);

        var withNavs = new ActivityUserRoleAssignment
        {
            ActivityId = Guid.NewGuid(),
            UserId = actingUserId,
            User = new User { FirstName = "Ada", LastName = "Parent" },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType { Name = "Líder", Description = "d" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType { Name = "Confirmado", Color = "#0f0" },
        };
        var withoutNavs = new ActivityUserRoleAssignment
        {
            ActivityId = Guid.NewGuid(),
            UserId = childId,
            User = new User { FirstName = "Kid", LastName = "One" },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = null!,
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = null!,
        };
        activities
            .GetAssignmentsForUsersByEventAsync(
                Arg.Any<IReadOnlyList<Guid>>(),
                eventId,
                Arg.Any<CancellationToken>()
            )
            .Returns([withNavs, withoutNavs]);

        var result = await sut.GetHouseholdAssignmentsAsync(
            actingUserId,
            eventId,
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        result[0].FirstName.Should().Be("Ada");
        result[0].RoleName.Should().Be("Líder");
        result[0].StatusName.Should().Be("Confirmado");
        result[1].RoleName.Should().BeEmpty();
        result[1].StatusName.Should().BeEmpty();
        await users
            .Received(1)
            .ListChildrenWithDetailsAsync(actingUserId, Arg.Any<CancellationToken>());
    }
}
