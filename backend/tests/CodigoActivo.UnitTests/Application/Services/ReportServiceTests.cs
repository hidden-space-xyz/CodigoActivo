using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

/// <summary>
/// Unit tests for <see cref="ReportService"/>. All repositories are NSubstitute doubles; the service
/// performs pure in-memory aggregation over the graphs they return, so tests feed representative
/// object graphs and assert the computed shape and every error code.
/// </summary>
public sealed class ReportServiceTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRoleTypeRepository roleTypes = Substitute.For<IActivityRoleTypeRepository>();
    private readonly IAssignmentStatusTypeRepository statusTypes = Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IAnnouncementRepository announcements = Substitute.For<IAnnouncementRepository>();
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ReportService sut;

    // Deterministic role ids: two known roles plus one "ghost" role absent from the roleTypes catalog.
    private static readonly Guid AlphaRoleId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GhostRoleId = new("33333333-3333-3333-3333-333333333333");

    private static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    private static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    private static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;

    public ReportServiceTests()
    {
        sut = new ReportService(events, roleTypes, statusTypes, activities, resources, announcements, partners, users);
    }

    // ---- helpers -----------------------------------------------------------

    private void HasRoleTypes(params (Guid Id, string Name)[] roles) =>
        roleTypes.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ActivityRoleType>)roles
                .Select(r => new ActivityRoleType { Id = r.Id, Name = r.Name })
                .ToList());

    private void HasStatusTypes(params (Guid Id, string Name)[] statuses) =>
        statusTypes.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AssignmentStatusType>)statuses
                .Select(s => new AssignmentStatusType { Id = s.Id, Name = s.Name, Color = "#fff" })
                .ToList());

    private void EventGraph(Guid id, Event? ev) =>
        events.GetWithActivitiesAndAssignmentsAsync(id, Arg.Any<CancellationToken>()).Returns(ev);

    private void ActivityGraph(Guid id, Activity? activity) =>
        activities.GetWithAssignmentsAndUsersAsync(id, Arg.Any<CancellationToken>()).Returns(activity);

    private static ActivityUserRoleAssignment Asg(Guid userId, Guid roleId, Guid statusId) =>
        new() { UserId = userId, ActivityRoleTypeId = roleId, AssignmentStatusId = statusId };

    private static ActivityAllowedRoleType Allowed(Guid roleId, string? name) =>
        new()
        {
            ActivityRoleTypeId = roleId,
            ActivityRoleType = new ActivityRoleType { Id = roleId, Name = name! },
        };

    private static Activity ActivityWith(
        IEnumerable<ActivityAllowedRoleType> allowed,
        IEnumerable<ActivityUserRoleAssignment> assignments,
        string title = "Act"
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            AllowedRoleTypes = allowed.ToList(),
            Assignments = assignments.ToList(),
        };

    private static User NewUser(string first, User? parent = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + "-last",
            Email = first + "@test.local",
            Phone = "555-" + first,
            Parent = parent,
            ParentId = parent?.Id,
        };

    private static ActivityUserRoleAssignment FullAsg(User user, Guid roleId, string roleName, Guid statusId, string statusName) =>
        new()
        {
            UserId = user.Id,
            User = user,
            ActivityRoleTypeId = roleId,
            ActivityRoleType = new ActivityRoleType { Id = roleId, Name = roleName },
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType { Id = statusId, Name = statusName, Color = "#000" },
        };

    // ---- GetEventSummaryAsync ---------------------------------------------

    [Fact]
    public async Task GetEventSummaryAsync_returns_not_found_when_event_missing()
    {
        var id = Guid.NewGuid();
        EventGraph(id, null);

        var result = await sut.GetEventSummaryAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await roleTypes.DidNotReceiveWithAnyArgs().GetAllAsync(default);
    }

    [Fact]
    public async Task GetEventSummaryAsync_aggregates_counts_status_breakdown_and_distinct_volunteers()
    {
        var eventId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var activity1 = ActivityWith(
            allowed: [Allowed(AlphaRoleId, "Alpha"), Allowed(BetaRoleId, "Beta")],
            assignments:
            [
                Asg(user1, AlphaRoleId, Confirmed),
                Asg(user2, AlphaRoleId, Confirmed),
                Asg(user1, BetaRoleId, Confirmed), // user1 again -> distinct volunteers unaffected
            ]);
        var activity2 = ActivityWith(
            allowed: [Allowed(BetaRoleId, "Beta"), Allowed(GhostRoleId, "unused")],
            assignments:
            [
                Asg(user3, BetaRoleId, Requested),
                Asg(user2, GhostRoleId, Denied),
            ]);

        var ev = new Event { Id = eventId, Title = "Feria", Activities = [activity1, activity2] };
        EventGraph(eventId, ev);
        // Ghost role deliberately absent from catalog -> its RoleTypeName resolves to null.
        HasRoleTypes((AlphaRoleId, "Alpha"), (BetaRoleId, "Beta"));

        var result = await sut.GetEventSummaryAsync(eventId);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.EventId.Should().Be(eventId);
        summary.Title.Should().Be("Feria");
        summary.ActivitiesCount.Should().Be(2);
        summary.TotalAssignments.Should().Be(5);
        summary.RequestedAssignments.Should().Be(1);
        summary.ConfirmedAssignments.Should().Be(3);
        summary.DeniedAssignments.Should().Be(1);
        summary.DistinctVolunteers.Should().Be(3);

        // Breakdown covers the distinct allowed roles across activities, ordered by name (Ordinal, null first).
        summary.RoleTypeBreakdown.Should().HaveCount(3);
        summary.RoleTypeBreakdown[0].Should().BeEquivalentTo(
            new EventRoleTypeSummaryResponse(GhostRoleId, null, 0));
        summary.RoleTypeBreakdown[1].Should().BeEquivalentTo(
            new EventRoleTypeSummaryResponse(AlphaRoleId, "Alpha", 2));
        summary.RoleTypeBreakdown[2].Should().BeEquivalentTo(
            new EventRoleTypeSummaryResponse(BetaRoleId, "Beta", 1));
    }

    [Fact]
    public async Task GetEventSummaryAsync_returns_empty_breakdown_when_no_activities()
    {
        var eventId = Guid.NewGuid();
        var ev = new Event { Id = eventId, Title = "Vacío", Activities = [] };
        EventGraph(eventId, ev);
        HasRoleTypes();

        var result = await sut.GetEventSummaryAsync(eventId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActivitiesCount.Should().Be(0);
        result.Value.TotalAssignments.Should().Be(0);
        result.Value.DistinctVolunteers.Should().Be(0);
        result.Value.RoleTypeBreakdown.Should().BeEmpty();
    }

    // ---- GetEventAssignmentsAsync -----------------------------------------

    [Fact]
    public async Task GetEventAssignmentsAsync_returns_not_found_when_event_missing()
    {
        var id = Guid.NewGuid();
        EventGraph(id, null);

        var result = await sut.GetEventAssignmentsAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task GetEventAssignmentsAsync_flattens_items_and_resolves_names_with_null_fallback()
    {
        var eventId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var activity = ActivityWith(
            allowed: [Allowed(AlphaRoleId, "Alpha")],
            assignments:
            [
                Asg(user1, AlphaRoleId, Confirmed),
                Asg(user2, GhostRoleId, Requested), // ghost role + missing status -> null names
            ],
            title: "Taller");

        var ev = new Event { Id = eventId, Title = "Congreso", Activities = [activity] };
        EventGraph(eventId, ev);
        HasRoleTypes((AlphaRoleId, "Alpha"));
        HasStatusTypes((Confirmed, "Confirmado")); // Requested deliberately absent -> null status name

        var result = await sut.GetEventAssignmentsAsync(eventId);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be(eventId);
        result.Value.Title.Should().Be("Congreso");
        result.Value.Items.Should().HaveCount(2);

        var confirmed = result.Value.Items.Single(i => i.UserId == user1);
        confirmed.ActivityTitle.Should().Be("Taller");
        confirmed.RoleTypeId.Should().Be(AlphaRoleId);
        confirmed.RoleTypeName.Should().Be("Alpha");
        confirmed.StatusId.Should().Be(Confirmed);
        confirmed.StatusName.Should().Be("Confirmado");

        var unresolved = result.Value.Items.Single(i => i.UserId == user2);
        unresolved.RoleTypeName.Should().BeNull();
        unresolved.StatusName.Should().BeNull();
    }

    // ---- GetActivityAssignmentsAsync --------------------------------------

    [Fact]
    public async Task GetActivityAssignmentsAsync_returns_not_found_when_activity_missing()
    {
        var id = Guid.NewGuid();
        ActivityGraph(id, null);

        var result = await sut.GetActivityAssignmentsAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task GetActivityAssignmentsAsync_builds_rows_parents_and_confirmed_breakdown()
    {
        var parentP = NewUser("Padre"); // not signed up -> should be appended once as a non-signup row
        var parentQ = NewUser("Madre"); // signed up -> must NOT be appended as a parent row

        var child1 = NewUser("Hijo1", parentP);
        var child2 = NewUser("Hijo2", parentP); // same parent -> parent added only once
        var child3 = NewUser("Hijo3", parentQ); // parent already signed up -> skipped
        var solo = NewUser("Solo"); // no parent -> nothing extra

        var activity = ActivityWith(
            allowed: [Allowed(AlphaRoleId, "Alpha"), Allowed(BetaRoleId, "Beta")],
            assignments:
            [
                FullAsg(child1, AlphaRoleId, "Alpha", Confirmed, "Confirmado"),
                FullAsg(child2, BetaRoleId, "Beta", Requested, "Solicitado"),
                FullAsg(child3, AlphaRoleId, "Alpha", Confirmed, "Confirmado"),
                FullAsg(parentQ, BetaRoleId, "Beta", Denied, "Denegado"),
                FullAsg(solo, AlphaRoleId, "Alpha", Confirmed, "Confirmado"),
            ],
            title: "Limpieza");
        var activityId = activity.Id;
        ActivityGraph(activityId, activity);

        var result = await sut.GetActivityAssignmentsAsync(activityId);

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.ActivityId.Should().Be(activityId);
        report.Title.Should().Be("Limpieza");
        report.TotalSignups.Should().Be(5);

        // 5 signed-up rows + parentP appended once (parentQ signed up, so not appended).
        report.Rows.Should().HaveCount(6);
        report.Rows.Count(r => r.SignedUp).Should().Be(5);

        var parentRow = report.Rows.Single(r => !r.SignedUp);
        parentRow.UserId.Should().Be(parentP.Id);
        parentRow.FirstName.Should().Be("Padre");
        parentRow.Email.Should().Be(parentP.Email);
        parentRow.RoleTypeId.Should().BeNull();
        parentRow.RoleTypeName.Should().BeNull();
        parentRow.StatusId.Should().BeNull();
        parentRow.StatusName.Should().BeNull();

        var childRow = report.Rows.Single(r => r.UserId == child1.Id);
        childRow.SignedUp.Should().BeTrue();
        childRow.RoleTypeId.Should().Be(AlphaRoleId);
        childRow.RoleTypeName.Should().Be("Alpha");
        childRow.StatusName.Should().Be("Confirmado");
        childRow.ParentId.Should().Be(parentP.Id);

        // Confirmed-only breakdown, ordered by role name: Alpha (child1, child3, solo) = 3, Beta = 0.
        report.RoleTypeBreakdown.Should().HaveCount(2);
        report.RoleTypeBreakdown[0].Should().BeEquivalentTo(
            new ActivityRoleTypeSummaryResponse(AlphaRoleId, "Alpha", 3));
        report.RoleTypeBreakdown[1].Should().BeEquivalentTo(
            new ActivityRoleTypeSummaryResponse(BetaRoleId, "Beta", 0));
    }

    [Fact]
    public async Task GetActivityAssignmentsAsync_handles_activity_with_no_assignments()
    {
        var activity = ActivityWith(allowed: [], assignments: [], title: "Sola");
        ActivityGraph(activity.Id, activity);

        var result = await sut.GetActivityAssignmentsAsync(activity.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSignups.Should().Be(0);
        result.Value.Rows.Should().BeEmpty();
        result.Value.RoleTypeBreakdown.Should().BeEmpty();
    }

    // ---- GetDashboardSummaryAsync -----------------------------------------

    [Fact]
    public async Task GetDashboardSummaryAsync_maps_each_repository_count_in_order()
    {
        events.CountAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>()).Returns(1);
        activities.CountAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>()).Returns(2);
        resources.CountAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>()).Returns(3);
        announcements.CountAsync(Arg.Any<Expression<Func<Announcement, bool>>>(), Arg.Any<CancellationToken>()).Returns(4);
        partners.CountAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>()).Returns(5);
        users.CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(6);

        var result = await sut.GetDashboardSummaryAsync();

        result.Should().BeEquivalentTo(new DashboardSummaryResponse(1, 2, 3, 4, 5, 6));
    }
}
