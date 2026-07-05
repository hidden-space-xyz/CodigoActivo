using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

/// <summary>
/// Assignment, household and overlap coverage for <see cref="ActivityService"/>. CRUD and catalog
/// coverage lives in <c>ActivityServiceTests</c>. Signup-window reads run against the real
/// <see cref="FakeQueryExecutor"/> so <c>EnsureSignupOpenAsync</c> is exercised for real.
/// </summary>
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

    // clock.UtcNow default is 2026-07-04 12:00Z; these windows bracket it / exclude it.
    private static readonly DateTimeOffset OpenStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OpenEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PastStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PastEnd = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

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
        activities.Query().Returns(
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
            .FindAsync(Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Requested,
                Name = name,
                Color = "#000",
            });

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

    // ---- AssignAsync -------------------------------------------------------

    [Fact]
    public async Task AssignAsync_returns_not_found_when_activity_window_missing()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.AssignAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignAsync_returns_signup_closed_when_outside_window_for_member()
    {
        var activityId = Guid.NewGuid();
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignAsync_returns_role_not_allowed_when_role_not_in_activity()
    {
        var activityId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(false);

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignAsync_returns_conflict_when_assignment_already_exists()
    {
        var activityId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd);
        AllowedRoleExists(true);
        ExistingAssignment(Assignment(Guid.NewGuid(), activityId));

        var result = await sut.AssignAsync(
            activityId,
            Guid.NewGuid(),
            new AssignRequest(Guid.NewGuid()),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentAlreadyExists);
        await activities.DidNotReceiveWithAnyArgs().AddAssignmentAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignAsync_persists_assignment_and_returns_requested_status()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        // Admin bypasses the window even though it is closed.
        HasActivityWindow(activityId, PastStart, PastEnd);
        AllowedRoleExists(true);
        ExistingAssignment(null);
        RequestedStatusNamed("Solicitado");

        var result = await sut.AssignAsync(
            activityId,
            userId,
            new AssignRequest(roleId),
            isAdmin: true
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.ActivityId.Should().Be(activityId);
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        result.Value.Status.Name.Should().Be("Solicitado");
        await activities.Received(1).AddAssignmentAsync(
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

    // ---- AssignHouseholdAsync ---------------------------------------------

    [Fact]
    public async Task AssignHouseholdAsync_returns_required_when_no_assignments()
    {
        var result = await sut.AssignHouseholdAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AssignHouseholdRequest(new List<HouseholdAssignmentRequest>()),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdAssignmentsRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignHouseholdAsync_propagates_signup_closed_for_member()
    {
        var activityId = Guid.NewGuid();
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.AssignHouseholdAsync(
            activityId,
            Guid.NewGuid(),
            new AssignHouseholdRequest(
                new List<HouseholdAssignmentRequest> { new(Guid.NewGuid(), Guid.NewGuid()) }
            ),
            isAdmin: false
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignHouseholdAsync_returns_member_not_allowed_when_not_in_household()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd, Guid.NewGuid());
        users.Query().Returns(new List<User>().AsQueryable());

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest(
                new List<HouseholdAssignmentRequest> { new(strangerId, Guid.NewGuid()) }
            ),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.ActivityHouseholdMemberNotAllowed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignHouseholdAsync_returns_role_not_allowed_when_role_unknown()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd, Guid.NewGuid());

        var result = await sut.AssignHouseholdAsync(
            activityId,
            actingUserId,
            new AssignHouseholdRequest(
                // acting user only (no household check), role not in the activity's allowed set.
                new List<HouseholdAssignmentRequest> { new(actingUserId, Guid.NewGuid()) }
            ),
            isAdmin: true
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AssignHouseholdAsync_creates_missing_assignments_and_skips_existing()
    {
        var activityId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        HasActivityWindow(activityId, OpenStart, OpenEnd, roleId);
        users.Query().Returns(
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
        // The child is already assigned, so only the acting user is created.
        activities.QueryAssignments().Returns(
            new List<ActivityUserRoleAssignment>
            {
                new() { UserId = childId, ActivityId = activityId },
            }.AsQueryable()
        );
        // statuses.FindAsync is left unstubbed (returns null) to exercise the empty-name fallback.

        var request = new AssignHouseholdRequest(
            new List<HouseholdAssignmentRequest>
            {
                new(actingUserId, roleId),
                new(actingUserId, roleId), // duplicate, deduped by UserId
                new(childId, roleId),
            }
        );

        var result = await sut.AssignHouseholdAsync(activityId, actingUserId, request, isAdmin: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(actingUserId);
        result.Value[0].Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        result.Value[0].Status.Name.Should().BeEmpty();
        await activities.Received(1).AddAssignmentAsync(
            Arg.Is<ActivityUserRoleAssignment>(a => a.UserId == actingUserId),
            Arg.Any<CancellationToken>()
        );
        await activities.DidNotReceive().AddAssignmentAsync(
            Arg.Is<ActivityUserRoleAssignment>(a => a.UserId == childId),
            Arg.Any<CancellationToken>()
        );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- UnassignAsync -----------------------------------------------------

    [Fact]
    public async Task UnassignAsync_returns_not_found_when_assignment_missing()
    {
        ExistingAssignment(null);

        var result = await sut.UnassignAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UnassignAsync_removes_without_window_check_for_admin()
    {
        var assignment = Assignment(Guid.NewGuid(), Guid.NewGuid());
        ExistingAssignment(assignment);

        var result = await sut.UnassignAsync(assignment.ActivityId, assignment.UserId, isAdmin: true);

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignAsync_returns_signup_closed_for_member_when_window_closed()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        ExistingAssignment(assignment);
        HasActivityWindow(activityId, PastStart, PastEnd);

        var result = await sut.UnassignAsync(activityId, assignment.UserId, isAdmin: false);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivitySignupClosed);
        activities.DidNotReceiveWithAnyArgs().RemoveAssignment(default!);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UnassignAsync_removes_for_member_when_window_open()
    {
        var activityId = Guid.NewGuid();
        var assignment = Assignment(Guid.NewGuid(), activityId);
        ExistingAssignment(assignment);
        HasActivityWindow(activityId, OpenStart, OpenEnd);

        var result = await sut.UnassignAsync(activityId, assignment.UserId, isAdmin: false);

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- ChangeStatusAsync -------------------------------------------------

    [Fact]
    public async Task ChangeStatusAsync_returns_not_found_when_assignment_missing()
    {
        ExistingAssignment(null);

        var result = await sut.ChangeStatusAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentStatusRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeStatusAsync_returns_not_found_when_status_missing()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        statuses.FindAsync(Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((AssignmentStatusType?)null);

        var result = await sut.ChangeStatusAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentStatusRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AssignmentStatusTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeStatusAsync_updates_status_and_persists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        // ActivityRoleType left null to exercise the null-conditional role name.
        var assignment = Assignment(userId, activityId);
        ExistingAssignment(assignment);
        statuses.FindAsync(Arg.Any<Expression<Func<AssignmentStatusType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new AssignmentStatusType { Id = statusId, Name = "Confirmado", Color = "#0f0" });

        var result = await sut.ChangeStatusAsync(
            activityId,
            userId,
            new ChangeAssignmentStatusRequest(statusId)
        );

        result.IsSuccess.Should().BeTrue();
        assignment.AssignmentStatusId.Should().Be(statusId);
        result.Value.Status.Id.Should().Be(statusId);
        result.Value.Status.Name.Should().Be("Confirmado");
        result.Value.RoleTypeName.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- ChangeRoleAsync ---------------------------------------------------

    [Fact]
    public async Task ChangeRoleAsync_returns_not_found_when_assignment_missing()
    {
        ExistingAssignment(null);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeRoleAsync_returns_role_not_allowed_when_role_not_in_activity()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        AllowedRoleExists(false);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleNotAllowed);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeRoleAsync_returns_role_type_not_found_when_role_missing()
    {
        ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        AllowedRoleExists(true);
        roleTypes.FindAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((ActivityRoleType?)null);

        var result = await sut.ChangeRoleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ChangeAssignmentRoleRequest(Guid.NewGuid())
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeRoleAsync_updates_role_and_persists()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        // AssignmentStatus left null to exercise the empty-name fallback.
        var assignment = Assignment(userId, activityId);
        ExistingAssignment(assignment);
        AllowedRoleExists(true);
        roleTypes.FindAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new ActivityRoleType { Id = roleId, Name = "Líder", Description = "d" });

        var result = await sut.ChangeRoleAsync(
            activityId,
            userId,
            new ChangeAssignmentRoleRequest(roleId)
        );

        result.IsSuccess.Should().BeTrue();
        // The role id is part of the composite key, so the change is a remove + re-insert that
        // carries the original status, not an in-place mutation.
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

    // ---- VerifyTimeOverlapsAsync ------------------------------------------

    [Fact]
    public async Task VerifyTimeOverlapsAsync_returns_not_found_when_activity_missing()
    {
        activities.FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.VerifyTimeOverlapsAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_reports_overlaps_excluding_the_target_activity()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var target = OverlapActivity(activityId, 10, 12);
        activities.FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(target);

        var overlappingId = Guid.NewGuid();
        activities.GetUserAssignmentsAsync(userId, Arg.Any<CancellationToken>()).Returns(
            new List<ActivityUserRoleAssignment>
            {
                // Same activity is skipped even though times coincide.
                new() { ActivityId = activityId, Activity = OverlapActivity(activityId, 10, 12) },
                // Overlaps the target window (11:00-13:00 vs 10:00-12:00).
                new() { ActivityId = overlappingId, Activity = OverlapActivity(overlappingId, 11, 13, "Choque") },
            }
        );

        var result = await sut.VerifyTimeOverlapsAsync(activityId, userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeTrue();
        result.Value.Overlaps.Should().ContainSingle();
        result.Value.Overlaps[0].ActivityId.Should().Be(overlappingId);
        result.Value.Overlaps[0].Title.Should().Be("Choque");
    }

    [Fact]
    public async Task VerifyTimeOverlapsAsync_reports_none_when_disjoint()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        activities.FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(OverlapActivity(activityId, 10, 12));
        activities.GetUserAssignmentsAsync(userId, Arg.Any<CancellationToken>()).Returns(
            new List<ActivityUserRoleAssignment>
            {
                new() { ActivityId = Guid.NewGuid(), Activity = OverlapActivity(Guid.NewGuid(), 13, 14) },
            }
        );

        var result = await sut.VerifyTimeOverlapsAsync(activityId, userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    private static Activity OverlapActivity(Guid id, int startHour, int endHour, string title = "Act") =>
        new()
        {
            Id = id,
            Title = title,
            Description = "{}",
            Location = "l",
            ActivityStartsAt = new DateTimeOffset(2026, 7, 10, startHour, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = new DateTimeOffset(2026, 7, 10, endHour, 0, 0, TimeSpan.Zero),
        };

    // ---- GetHouseholdAssignmentsAsync -------------------------------------

    [Fact]
    public async Task GetHouseholdAssignmentsAsync_maps_children_and_falls_back_on_null_navigations()
    {
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        users.ListChildrenWithDetailsAsync(actingUserId, Arg.Any<CancellationToken>()).Returns(
            new List<User> { new() { Id = childId, FirstName = "Kid", LastName = "One" } }
        );

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
        activities.GetAssignmentsForUsersByEventAsync(
            Arg.Any<IReadOnlyList<Guid>>(),
            eventId,
            Arg.Any<CancellationToken>()
        ).Returns(new List<ActivityUserRoleAssignment> { withNavs, withoutNavs });

        var result = await sut.GetHouseholdAssignmentsAsync(actingUserId, eventId);

        result.Should().HaveCount(2);
        result[0].FirstName.Should().Be("Ada");
        result[0].RoleName.Should().Be("Líder");
        result[0].StatusName.Should().Be("Confirmado");
        result[1].RoleName.Should().BeEmpty();
        result[1].StatusName.Should().BeEmpty();
        await users.Received(1).ListChildrenWithDetailsAsync(actingUserId, Arg.Any<CancellationToken>());
    }
}
