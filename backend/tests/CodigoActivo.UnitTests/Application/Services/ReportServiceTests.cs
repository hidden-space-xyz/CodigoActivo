using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class ReportServiceTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ReportService sut;

    private static readonly Guid QueriedEventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid AlphaRoleId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GhostRoleId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid IdleRoleId = new("44444444-4444-4444-4444-444444444444");

    private static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    private static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    private static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;

    public ReportServiceTests()
    {
        sut = new ReportService(
            events,
            roleTypes,
            activities,
            resources,
            announcements,
            partners,
            users,
            new FakeQueryExecutor()
        );
    }

    private void HasRoleTypes(params (Guid Id, string Name)[] roles) =>
        roleTypes
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(roles.Select(r => new ActivityRoleType { Id = r.Id, Name = r.Name }).ToList());

    private void EventGraph(Guid id, Event? ev) =>
        events.GetWithActivitiesAndAssignmentsAsync(id, Arg.Any<CancellationToken>()).Returns(ev);

    private static ActivityUserRoleAssignment Asg(Guid userId, Guid roleId, Guid statusId) =>
        new()
        {
            UserId = userId,
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
        };

    private static Activity ActivityWith(
        IEnumerable<ActivityUserRoleAssignment> assignments,
        string title = "Act"
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
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
            BirthDate = new DateOnly(1990, 6, 15),
            Parent = parent,
            ParentId = parent?.Id,
            UserType = new UserType { Name = "Socio", Color = "#EF4444" },
        };

    [Fact]
    public async Task GetEventSummaryAsync_EventMissing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        EventGraph(id, null);

        var result = await sut.GetEventSummaryAsync(id, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await roleTypes
            .DidNotReceiveWithAnyArgs()
            .GetAllAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetEventSummaryAsync_MultipleActivitiesAndAssignments_AggregatesCountsAndBreakdown()
    {
        var eventId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var activity1 = ActivityWith([
            Asg(user1, AlphaRoleId, Confirmed),
            Asg(user2, AlphaRoleId, Confirmed),
            Asg(user1, BetaRoleId, Confirmed),
        ]);
        var activity2 = ActivityWith([
            Asg(user3, BetaRoleId, Requested),
            Asg(user2, GhostRoleId, Denied),
        ]);

        var ev = new Event
        {
            Id = eventId,
            Title = "Feria",
            Activities = [activity1, activity2],
        };
        EventGraph(eventId, ev);
        HasRoleTypes((AlphaRoleId, "Alpha"), (IdleRoleId, "Idle"), (BetaRoleId, "Beta"));

        var result = await sut.GetEventSummaryAsync(eventId, TestContext.Current.CancellationToken);

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

        summary.RoleTypeBreakdown.Should().HaveCount(3);
        summary
            .RoleTypeBreakdown[0]
            .Should()
            .BeEquivalentTo(new EventRoleTypeSummaryResponse(AlphaRoleId, "Alpha", 2));
        summary
            .RoleTypeBreakdown[1]
            .Should()
            .BeEquivalentTo(new EventRoleTypeSummaryResponse(BetaRoleId, "Beta", 1));
        summary
            .RoleTypeBreakdown[2]
            .Should()
            .BeEquivalentTo(new EventRoleTypeSummaryResponse(IdleRoleId, "Idle", 0));
        summary.RoleTypeBreakdown.Should().NotContain(r => r.RoleTypeId == GhostRoleId);
    }

    [Fact]
    public async Task GetEventSummaryAsync_NoActivities_ReturnsEmptyBreakdown()
    {
        var eventId = Guid.NewGuid();
        var ev = new Event
        {
            Id = eventId,
            Title = "Vacío",
            Activities = [],
        };
        EventGraph(eventId, ev);
        HasRoleTypes();

        var result = await sut.GetEventSummaryAsync(eventId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActivitiesCount.Should().Be(0);
        result.Value.TotalAssignments.Should().Be(0);
        result.Value.DistinctVolunteers.Should().Be(0);
        result.Value.RoleTypeBreakdown.Should().BeEmpty();
    }

    private void HasEvent(Event? ev) =>
        events
            .FindAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ev);

    private void HasAssignments(params ActivityUserRoleAssignment[] assignments) =>
        activities.QueryAssignments().Returns(assignments.AsQueryable());

    private static ActivityUserRoleAssignment AttendeeAsg(
        User user,
        string activityTitle,
        DateTimeOffset startsAt,
        Guid roleId,
        string roleName,
        Guid statusId,
        string statusName,
        Guid? eventId = null,
        DateTimeOffset? signedUpAt = null,
        TimeSpan? duration = null
    ) =>
        new()
        {
            UserId = user.Id,
            User = user,
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                EventId = eventId ?? QueriedEventId,
                Title = activityTitle,
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt + (duration ?? TimeSpan.FromHours(2)),
            },
            ActivityRoleTypeId = roleId,
            ActivityRoleType = new ActivityRoleType { Id = roleId, Name = roleName },
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType
            {
                Id = statusId,
                Name = statusName,
                Color = "#fff",
            },
            CreatedAt = signedUpAt ?? startsAt.AddDays(-7),
        };

    [Fact]
    public async Task GetEventAttendeesAsync_EventMissing_ReturnsNotFound()
    {
        HasEvent(null);

        var result = await sut.GetEventAttendeesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        activities.DidNotReceive().QueryAssignments();
    }

    [Fact]
    public async Task GetEventAttendeesAsync_AssignmentsAcrossActivities_GroupsPerUserWithOrderedAssignments()
    {
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        var ana = NewUser("Ana", NewUser("Tutora"));
        var berto = NewUser("Berto");

        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            AttendeeAsg(
                berto,
                "Charla",
                when.AddHours(2),
                AlphaRoleId,
                "Alpha",
                Requested,
                "Solicitada"
            ),
            AttendeeAsg(ana, "Charla", when.AddHours(2), BetaRoleId, "Beta", Denied, "Rechazada"),
            AttendeeAsg(
                ana,
                "Taller",
                when,
                AlphaRoleId,
                "Alpha",
                Confirmed,
                "Confirmada",
                signedUpAt: when.AddDays(-3)
            ),
            AttendeeAsg(
                ana,
                "Otro evento",
                when,
                AlphaRoleId,
                "Alpha",
                Confirmed,
                "Confirmada",
                eventId: Guid.NewGuid()
            )
        );

        var result = await sut.GetEventAttendeesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.EventId.Should().Be(QueriedEventId);
        report.Title.Should().Be("Feria");
        report.Attendees.Should().HaveCount(2);

        var first = report.Attendees[0];
        first.UserId.Should().Be(ana.Id);
        first.FirstName.Should().Be("Ana");
        first.LastName.Should().Be("Ana-last");
        first.Email.Should().Be(ana.Email);
        first.Phone.Should().Be(ana.Phone);
        first.BirthDate.Should().Be(new DateOnly(1990, 6, 15));
        first.UserTypeName.Should().Be("Socio");
        first.UserTypeColor.Should().Be("#EF4444");
        first.Guardian.Should().NotBeNull();
        first.Guardian!.FirstName.Should().Be("Tutora");
        first.Guardian.LastName.Should().Be("Tutora-last");
        first.Guardian.Email.Should().Be("Tutora@test.local");
        first.Guardian.Phone.Should().Be("555-Tutora");
        first.Assignments.Should().HaveCount(2);
        first.Assignments[0].ActivityTitle.Should().Be("Taller");
        first.Assignments[0].ActivityStartsAt.Should().Be(when);
        first.Assignments[0].ActivityEndsAt.Should().Be(when.AddHours(2));
        first.Assignments[0].RoleTypeId.Should().Be(AlphaRoleId);
        first.Assignments[0].RoleTypeName.Should().Be("Alpha");
        first.Assignments[0].StatusId.Should().Be(Confirmed);
        first.Assignments[0].StatusName.Should().Be("Confirmada");
        first.Assignments[0].SignedUpAt.Should().Be(when.AddDays(-3));
        first.Assignments[0].HasTimeConflict.Should().BeFalse();
        first.Assignments[1].ActivityTitle.Should().Be("Charla");
        first.Assignments[1].StatusName.Should().Be("Rechazada");
        first.Assignments[1].SignedUpAt.Should().Be(when.AddHours(2).AddDays(-7));
        first.Assignments[1].HasTimeConflict.Should().BeFalse();

        var second = report.Attendees[1];
        second.UserId.Should().Be(berto.Id);
        second.Guardian.Should().BeNull();
        second.Assignments.Should().HaveCount(1);
        second.Assignments[0].ActivityTitle.Should().Be("Charla");
        second.Assignments[0].StatusName.Should().Be("Solicitada");
        second.Assignments[0].HasTimeConflict.Should().BeFalse();
    }

    [Fact]
    public async Task GetEventAttendeesAsync_OverlappingAssignments_FlagsConflictsExcludingDenied()
    {
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        var carla = NewUser("Carla");
        var dani = NewUser("Dani");

        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            AttendeeAsg(carla, "Taller A", when, AlphaRoleId, "Alpha", Confirmed, "Confirmada"),
            AttendeeAsg(
                carla,
                "Taller B",
                when.AddHours(1),
                AlphaRoleId,
                "Alpha",
                Requested,
                "Solicitada"
            ),
            AttendeeAsg(
                carla,
                "Taller C",
                when.AddMinutes(90),
                AlphaRoleId,
                "Alpha",
                Denied,
                "Rechazada",
                duration: TimeSpan.FromHours(1)
            ),
            AttendeeAsg(dani, "Taller A", when, AlphaRoleId, "Alpha", Confirmed, "Confirmada"),
            AttendeeAsg(
                dani,
                "Taller B",
                when.AddHours(1),
                AlphaRoleId,
                "Alpha",
                Denied,
                "Rechazada"
            )
        );

        var result = await sut.GetEventAttendeesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();

        var withConflict = result.Value.Attendees.Single(a => a.UserId == carla.Id);
        withConflict.Assignments.Should().HaveCount(3);
        withConflict.Assignments[0].HasTimeConflict.Should().BeTrue();
        withConflict.Assignments[1].HasTimeConflict.Should().BeTrue();
        withConflict.Assignments[2].HasTimeConflict.Should().BeFalse();

        var withoutConflict = result.Value.Attendees.Single(a => a.UserId == dani.Id);
        withoutConflict.Assignments.Should().HaveCount(2);
        withoutConflict.Assignments.Should().OnlyContain(a => !a.HasTimeConflict);
    }

    [Fact]
    public async Task GetEventAttendeesAsync_NoAssignments_ReturnsEmptyAttendees()
    {
        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments();

        var result = await sut.GetEventAttendeesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Attendees.Should().BeEmpty();
    }

    private static User BadgeUser(
        string first,
        string last,
        string typeName,
        string typeColor,
        DateTimeOffset createdAt,
        User? parent = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Phone = "600-" + first,
            CreatedAt = createdAt,
            Parent = parent,
            ParentId = parent?.Id,
            UserType = new UserType { Name = typeName, Color = typeColor },
        };

    private static ActivityUserRoleAssignment BadgeAsg(
        User user,
        string activityTitle,
        DateTimeOffset startsAt,
        Guid statusId,
        Guid? eventId = null
    ) =>
        new()
        {
            UserId = user.Id,
            User = user,
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                EventId = eventId ?? QueriedEventId,
                Title = activityTitle,
                ActivityStartsAt = startsAt,
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            AssignmentStatusId = statusId,
        };

    [Fact]
    public async Task GetEventBadgesAsync_EventMissing_ReturnsNotFound()
    {
        HasEvent(null);

        var result = await sut.GetEventBadgesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        activities.DidNotReceive().QueryAssignments();
    }

    [Fact]
    public async Task GetEventBadgesAsync_ConfirmedAssignmentsWithGuardian_GroupsPerUser()
    {
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var parent = BadgeUser("Marta", "Miembro", "Socio", "#EF4444", createdAt);
        var child = BadgeUser("Mateo", "Miembro", "Participante", "#FFFFFF", createdAt, parent);
        var adult = BadgeUser("Ada", "Admin", "Socio", "#EF4444", createdAt);

        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            BadgeAsg(adult, "Charla", when.AddHours(2), Confirmed),
            BadgeAsg(adult, "Taller", when, Confirmed),
            BadgeAsg(adult, "Taller", when, Confirmed),
            BadgeAsg(adult, "Otro evento", when, Confirmed, eventId: Guid.NewGuid()),
            BadgeAsg(child, "Taller infantil", when, Confirmed),
            BadgeAsg(child, "Cuentacuentos", when, Requested),
            BadgeAsg(parent, "Charla", when, Denied)
        );

        var result = await sut.GetEventBadgesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.EventId.Should().Be(QueriedEventId);
        report.Title.Should().Be("Feria");
        report.Badges.Should().HaveCount(2);

        var adultBadge = report.Badges[0];
        adultBadge.UserId.Should().Be(adult.Id);
        adultBadge.FirstName.Should().Be("Ada");
        adultBadge.LastName.Should().Be("Admin");
        adultBadge.UserTypeName.Should().Be("Socio");
        adultBadge.UserTypeColor.Should().Be("#EF4444");
        adultBadge.CreatedAt.Should().Be(createdAt);
        adultBadge.Guardian.Should().BeNull();
        adultBadge.Activities.Should().Equal("Taller", "Taller", "Charla");

        var childBadge = report.Badges[1];
        childBadge.UserId.Should().Be(child.Id);
        childBadge.UserTypeName.Should().Be("Participante");
        childBadge.Guardian.Should().NotBeNull();
        childBadge.Guardian!.FirstName.Should().Be("Marta");
        childBadge.Guardian.LastName.Should().Be("Miembro");
        childBadge.Guardian.Phone.Should().Be("600-Marta");
        childBadge.Activities.Should().Equal("Taller infantil");
    }

    [Fact]
    public async Task GetEventBadgesAsync_NoConfirmedAssignments_ReturnsEmptyBadges()
    {
        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments();

        var result = await sut.GetEventBadgesAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Badges.Should().BeEmpty();
    }

    private static Activity RosterActivity(
        string title,
        DateTimeOffset startsAt,
        Guid? eventId = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId ?? QueriedEventId,
            Title = title,
            Location = "Sala " + title,
            ActivityStartsAt = startsAt,
            ActivityEndsAt = startsAt.AddHours(1),
        };

    private static ActivityUserRoleAssignment RosterAsg(
        User user,
        Activity activity,
        Guid statusId,
        Guid? roleTypeId = null,
        string roleName = "Participante"
    )
    {
        var roleId = roleTypeId ?? SeedIds.ActivityRoleTypes.Participant;
        return new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = roleId,
            ActivityRoleType = new ActivityRoleType { Id = roleId, Name = roleName },
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task GetEventRosterAsync_EventMissing_ReturnsNotFound()
    {
        HasEvent(null);

        var result = await sut.GetEventRosterAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        activities.DidNotReceive().QueryAssignments();
    }

    [Fact]
    public async Task GetEventRosterAsync_ConfirmedAssignments_GroupsByActivityWithGuardianContact()
    {
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var taller = RosterActivity("Taller", when);
        var charla = RosterActivity("Charla", when.AddHours(2));
        var foreignActivity = RosterActivity("Ajena", when, eventId: Guid.NewGuid());

        var parent = NewUser("Marta");
        var child = NewUser("Zoe", parent);
        child.Email = null;
        child.Phone = null;
        child.BirthDate = new DateOnly(2016, 3, 2);
        var adult = NewUser("Ada");
        var requestedUser = NewUser("Rita");
        var deniedUser = NewUser("Dario");

        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            RosterAsg(adult, charla, Confirmed),
            RosterAsg(adult, taller, Confirmed),
            RosterAsg(child, taller, Confirmed),
            RosterAsg(requestedUser, taller, Requested),
            RosterAsg(deniedUser, taller, Denied),
            RosterAsg(adult, foreignActivity, Confirmed)
        );

        var result = await sut.GetEventRosterAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.EventId.Should().Be(QueriedEventId);
        report.Title.Should().Be("Feria");
        report.Activities.Should().HaveCount(2);

        var first = report.Activities[0];
        first.ActivityId.Should().Be(taller.Id);
        first.Title.Should().Be("Taller");
        first.Location.Should().Be("Sala Taller");
        first.ActivityStartsAt.Should().Be(taller.ActivityStartsAt);
        first.ActivityEndsAt.Should().Be(taller.ActivityEndsAt);
        first.Participants.Should().HaveCount(2);
        first.Participants.Select(p => p.FirstName).Should().Equal("Ada", "Zoe");

        var adultRow = first.Participants[0];
        adultRow.Email.Should().Be("Ada@test.local");
        adultRow.Phone.Should().Be("555-Ada");
        adultRow.Guardian.Should().BeNull();

        var childRow = first.Participants[1];
        childRow.BirthDate.Should().Be(new DateOnly(2016, 3, 2));
        childRow.Email.Should().BeNull();
        childRow.Guardian.Should().NotBeNull();
        childRow.Guardian!.FirstName.Should().Be("Marta");
        childRow.Guardian.LastName.Should().Be("Marta-last");
        childRow.Guardian.Email.Should().Be("Marta@test.local");
        childRow.Guardian.Phone.Should().Be("555-Marta");

        var second = report.Activities[1];
        second.ActivityId.Should().Be(charla.Id);
        second.Participants.Should().ContainSingle(p => p.UserId == adult.Id);
    }

    [Fact]
    public async Task GetEventRosterAsync_MixedRoles_OrdersLeadersFirstAndKeepsHighestRolePerUser()
    {
        var when = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var taller = RosterActivity("Taller", when);

        var bruno = NewUser("Bruno");
        bruno.LastName = "Zeta";
        var ana = NewUser("Ana");
        ana.LastName = "Zeta";
        var zoe = NewUser("Zoe");
        zoe.LastName = "Alfa";
        var vera = NewUser("Vera");
        vera.LastName = "Alfa";

        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            RosterAsg(bruno, taller, Confirmed),
            RosterAsg(bruno, taller, Confirmed, SeedIds.ActivityRoleTypes.Leader, "Líder"),
            RosterAsg(vera, taller, Confirmed, SeedIds.ActivityRoleTypes.Volunteer, "Voluntario"),
            RosterAsg(zoe, taller, Confirmed),
            RosterAsg(ana, taller, Confirmed)
        );

        var result = await sut.GetEventRosterAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var participants = result.Value.Activities.Single().Participants;
        participants.Should().HaveCount(4);
        participants.Select(p => p.FirstName).Should().Equal("Bruno", "Vera", "Ana", "Zoe");
        participants
            .Select(p => p.RoleName)
            .Should()
            .Equal("Líder", "Voluntario", "Participante", "Participante");
    }

    [Fact]
    public async Task GetEventRosterAsync_NoConfirmedAssignments_ReturnsEmptyActivities()
    {
        HasEvent(new Event { Id = QueriedEventId, Title = "Feria" });
        HasAssignments();

        var result = await sut.GetEventRosterAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Activities.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_RepositoryCounts_MapsInOrder()
    {
        events
            .CountAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        activities
            .CountAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(2);
        resources
            .CountAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(3);
        announcements
            .CountAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(4);
        partners
            .CountAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(5);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(6);

        var result = await sut.GetDashboardSummaryAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new DashboardSummaryResponse(1, 2, 3, 4, 5, 6));
    }
}
