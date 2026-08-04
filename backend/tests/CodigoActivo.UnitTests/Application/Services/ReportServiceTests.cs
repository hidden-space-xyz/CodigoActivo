using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
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
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IEventCategoryTypeRepository eventCategoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IDashboardRepository dashboard = Substitute.For<IDashboardRepository>();
    private readonly TestClock clock = new(
        new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
        new DateOnly(2026, 7, 7)
    );
    private readonly FakeHybridCache cache = new();
    private readonly ReportService sut;

    private static readonly Guid QueriedEventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid AlphaRoleId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GhostRoleId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid IdleRoleId = new("44444444-4444-4444-4444-444444444444");

    private static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    private static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    private static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;

    private static readonly DateTimeOffset When = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    public ReportServiceTests()
    {
        sut = new ReportService(
            events,
            roleTypes,
            activities,
            users,
            resources,
            announcements,
            partners,
            eventCategoryTypes,
            dashboard,
            new FakeQueryExecutor(),
            clock,
            cache
        );
    }

    private void HasEvents(params Event[] list)
    {
        events.Query().Returns(list.AsQueryable());
    }

    private void HasRoleTypes(params ActivityRoleType[] list)
    {
        roleTypes.Query().Returns(list.AsQueryable());
    }

    private void HasAssignments(params ActivityUserRoleAssignment[] assignments)
    {
        activities.QueryAssignments().Returns(assignments.AsQueryable());
    }

    private void HasUsers(params User[] list)
    {
        users.Query().Returns(list.AsQueryable());
    }

    private static ActivityUserRoleAssignment SummaryAsg(
        Guid userId,
        Guid roleId,
        Guid statusId,
        Guid? eventId = null
    )
    {
        return new()
        {
            UserId = userId,
            ActivityId = Guid.NewGuid(),
            Activity = SummaryActivity(eventId ?? QueriedEventId),
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
        };
    }

    private static Activity SummaryActivity(Guid? eventId = null)
    {
        return new()
        {
            Title = "Actividad de prueba",
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            EventId = eventId ?? Guid.Empty,
        };
    }

    private static ActivityRoleType Role(
        Guid id,
        string name,
        IEnumerable<ActivityUserRoleAssignment> assignments
    )
    {
        return new()
        {
            Description = "Descripción de prueba",
            Id = id,
            Name = name,
            Assignments = [.. assignments.Where(a => a.ActivityRoleTypeId == id)],
        };
    }

    [Fact]
    public async Task GetEventSummaryAsync_EventMissing_ReturnsNotFound()
    {
        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = Guid.NewGuid(), Title = "Otra" });

        var result = await sut.GetEventSummaryAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        activities.DidNotReceive().QueryAssignments();
        roleTypes.DidNotReceive().Query();
    }

    [Fact]
    public async Task GetEventSummaryAsync_MixedStatusesAndRepeatedUsers_AggregatesCountsAndBreakdown()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var assignments = new[]
        {
            SummaryAsg(user1, AlphaRoleId, Confirmed),
            SummaryAsg(user2, AlphaRoleId, Confirmed),
            SummaryAsg(user1, BetaRoleId, Confirmed),
            SummaryAsg(user3, BetaRoleId, Requested),
            SummaryAsg(user2, GhostRoleId, Denied),
            SummaryAsg(user1, AlphaRoleId, Confirmed, Guid.NewGuid()),
        };

        HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
                Activities = [SummaryActivity(), SummaryActivity()],
            }
        );
        HasAssignments(assignments);
        HasRoleTypes(
            Role(AlphaRoleId, "Alpha", assignments),
            Role(IdleRoleId, "Idle", assignments),
            Role(BetaRoleId, "Beta", assignments)
        );

        var result = await sut.GetEventSummaryAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.EventId.Should().Be(QueriedEventId);
        summary.Title.Should().Be("Feria");
        summary.ActivitiesCount.Should().Be(2);
        summary.TotalAssignments.Should().Be(5);
        summary.RequestedAssignments.Should().Be(1);
        summary.ConfirmedAssignments.Should().Be(3);
        summary.DeniedAssignments.Should().Be(1);
        summary.DistinctVolunteers.Should().Be(3);
        summary
            .RoleTypeBreakdown.Should()
            .Equal(
                new EventRoleTypeSummaryResponse(AlphaRoleId, "Alpha", 2),
                new EventRoleTypeSummaryResponse(BetaRoleId, "Beta", 1),
                new EventRoleTypeSummaryResponse(IdleRoleId, "Idle", 0)
            );
        summary.RoleTypeBreakdown.Should().NotContain(r => r.RoleTypeId == GhostRoleId);
    }

    [Fact]
    public async Task GetEventSummaryAsync_NoAssignments_ReturnsZeroCounts()
    {
        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Vacío" });
        HasAssignments();
        HasRoleTypes();

        var result = await sut.GetEventSummaryAsync(
            QueriedEventId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ActivitiesCount.Should().Be(0);
        result.Value.TotalAssignments.Should().Be(0);
        result.Value.RequestedAssignments.Should().Be(0);
        result.Value.ConfirmedAssignments.Should().Be(0);
        result.Value.DeniedAssignments.Should().Be(0);
        result.Value.DistinctVolunteers.Should().Be(0);
        result.Value.RoleTypeBreakdown.Should().BeEmpty();
    }

    private static User NewUser(
        string first,
        User? parent = null,
        Guid? userTypeId = null,
        string? email = null,
        DateOnly? birthDate = null,
        string typeName = "Socio",
        Gender gender = Gender.Female
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + "-last",
            Email = email ?? (first + "@test.local"),
            Phone = "555-" + first,
            BirthDate = birthDate ?? new DateOnly(1990, 6, 15),
            Gender = gender,
            Parent = parent,
            ParentId = parent?.Id,
            UserTypeId = userTypeId ?? SeedIds.UserTypes.Member,
            UserType = new UserType { Description = "Descripción de prueba", Name = typeName, Color = "#EF4444" },
        };
    }

    private static ActivityUserRoleAssignment Enroll(
        User user,
        string activityTitle,
        DateTimeOffset startsAt,
        Guid statusId,
        string statusName,
        Guid? activityId = null,
        Guid? eventId = null,
        DateTimeOffset? signedUpAt = null,
        TimeSpan? duration = null
    )
    {
        var assignment = new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            ActivityId = activityId ?? Guid.NewGuid(),
            Activity = new Activity
            {
                Description = "Descripción de la actividad",
                Location = "Sala principal",
                EventId = eventId ?? QueriedEventId,
                Title = activityTitle,
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt + (duration ?? TimeSpan.FromHours(2)),
            },
            ActivityRoleTypeId = AlphaRoleId,
            ActivityRoleType = new ActivityRoleType { Description = "Descripción de prueba", Id = AlphaRoleId, Name = "Alpha" },
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType
            {
                Description = "Descripción de prueba",
                Id = statusId,
                Name = statusName,
                Color = "#fff",
            },
            CreatedAt = signedUpAt ?? startsAt.AddDays(-7),
        };
        user.Assignments.Add(assignment);
        return assignment;
    }

    private void HasConfirmedAttendees(params User[] attendees)
    {
        foreach (var attendee in attendees)
        {
            Enroll(attendee, "Taller", When, Confirmed, "Confirmada");
        }

        HasUsers(attendees);
    }

    [Fact]
    public async Task ListEventAttendeesAsync_AssignmentsAcrossActivities_GroupsPerUserWithOrderedAssignments()
    {
        var ana = NewUser("Ana", NewUser("Tutora"), gender: Gender.Other);
        var berto = NewUser("Berto");
        var outsider = NewUser("Zoe");
        Enroll(ana, "Charla", When.AddHours(2), Denied, "Rechazada");
        var taller = Enroll(
            ana,
            "Taller",
            When,
            Confirmed,
            "Confirmada",
            signedUpAt: When.AddDays(-3)
        );
        Enroll(ana, "Otro evento", When, Confirmed, "Confirmada", eventId: Guid.NewGuid());
        Enroll(berto, "Charla", When.AddHours(2), Requested, "Solicitada");
        Enroll(outsider, "Ajena", When, Confirmed, "Confirmada", eventId: Guid.NewGuid());
        HasUsers(berto, ana, outsider);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery(),
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(2);
        page.Items.Should().HaveCount(2);

        var first = page.Items[0];
        first.UserId.Should().Be(ana.Id);
        first.FirstName.Should().Be("Ana");
        first.LastName.Should().Be("Ana-last");
        first.Email.Should().Be("Ana@test.local");
        first.Phone.Should().Be("555-Ana");
        first.BirthDate.Should().Be(new DateOnly(1990, 6, 15));
        first.Gender.Should().Be(Gender.Other);
        first.UserTypeName.Should().Be("Socio");
        first.UserTypeColor.Should().Be("#EF4444");
        first.Guardian.Should().NotBeNull();
        first.Guardian.FirstName.Should().Be("Tutora");
        first.Guardian.LastName.Should().Be("Tutora-last");
        first.Guardian.Email.Should().Be("Tutora@test.local");
        first.Guardian.Phone.Should().Be("555-Tutora");
        first.Assignments.Should().HaveCount(2);
        first.Assignments[0].ActivityId.Should().Be(taller.ActivityId);
        first.Assignments[0].ActivityTitle.Should().Be("Taller");
        first.Assignments[0].ActivityStartsAt.Should().Be(When);
        first.Assignments[0].ActivityEndsAt.Should().Be(When.AddHours(2));
        first.Assignments[0].RoleTypeId.Should().Be(AlphaRoleId);
        first.Assignments[0].RoleTypeName.Should().Be("Alpha");
        first.Assignments[0].StatusId.Should().Be(Confirmed);
        first.Assignments[0].StatusName.Should().Be("Confirmada");
        first.Assignments[0].SignedUpAt.Should().Be(When.AddDays(-3));
        first.Assignments[0].HasTimeConflict.Should().BeFalse();
        first.Assignments[1].ActivityTitle.Should().Be("Charla");
        first.Assignments[1].StatusName.Should().Be("Rechazada");
        first.Assignments[1].HasTimeConflict.Should().BeFalse();

        var second = page.Items[1];
        second.UserId.Should().Be(berto.Id);
        second.Guardian.Should().BeNull();
        second.Assignments.Should().HaveCount(1);
        second.Assignments[0].ActivityTitle.Should().Be("Charla");
        second.Assignments[0].StatusName.Should().Be("Solicitada");
        second.Assignments[0].HasTimeConflict.Should().BeFalse();
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SearchMatchingGuardianName_FoldsAccentsAndFiltersUsers()
    {
        var zoe = NewUser("Zoe", NewUser("María"));
        var berto = NewUser("Berto");
        HasConfirmedAttendees(zoe, berto);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Search = "MARIA" },
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(zoe.Id);
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SearchMatchingOwnPhone_FiltersUsers()
    {
        var zoe = NewUser("Zoe", NewUser("María"));
        var berto = NewUser("Berto");
        HasConfirmedAttendees(zoe, berto);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Search = "555-berto" },
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(berto.Id);
    }

    [Fact]
    public async Task ListEventAttendeesAsync_ActivityAndStatusFilters_RequireOneAssignmentMatchingBoth()
    {
        var activityA = Guid.NewGuid();
        var activityB = Guid.NewGuid();
        var carla = NewUser("Carla");
        var dani = NewUser("Dani");
        Enroll(carla, "Taller A", When, Confirmed, "Confirmada", activityId: activityA);
        Enroll(carla, "Taller B", When.AddHours(1), Confirmed, "Confirmada", activityId: activityB);
        Enroll(dani, "Taller A", When, Requested, "Solicitada", activityId: activityA);
        Enroll(dani, "Taller B", When.AddHours(1), Confirmed, "Confirmada", activityId: activityB);
        HasUsers(carla, dani);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { ActivityId = activityA, StatusId = Confirmed },
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(1);
        var attendee = page.Items.Single();
        attendee.UserId.Should().Be(carla.Id);
        attendee.Assignments.Should().HaveCount(1);
        attendee.Assignments[0].ActivityId.Should().Be(activityA);
        attendee.Assignments[0].HasTimeConflict.Should().BeTrue();
    }

    [Fact]
    public async Task ListEventAttendeesAsync_UserTypeFilter_ReturnsOnlyMatchingUsers()
    {
        var ana = NewUser("Ana", userTypeId: SeedIds.UserTypes.Participant);
        var berto = NewUser("Berto");
        HasConfirmedAttendees(ana, berto);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { UserTypeId = SeedIds.UserTypes.Participant },
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(ana.Id);
    }

    [Fact]
    public async Task ListEventAttendeesAsync_OverlappingAssignments_FlagsConflictsExcludingDenied()
    {
        var carla = NewUser("Carla");
        var dani = NewUser("Dani");
        Enroll(carla, "Taller A", When, Confirmed, "Confirmada");
        Enroll(carla, "Taller B", When.AddHours(1), Requested, "Solicitada");
        Enroll(
            carla,
            "Taller C",
            When.AddMinutes(90),
            Denied,
            "Rechazada",
            duration: TimeSpan.FromHours(1)
        );
        Enroll(dani, "Taller A", When, Confirmed, "Confirmada");
        Enroll(dani, "Taller B", When.AddHours(1), Denied, "Rechazada");
        HasUsers(carla, dani);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery(),
            TestContext.Current.CancellationToken
        );

        var withConflict = page.Items.Single(a => a.UserId == carla.Id);
        withConflict.Assignments.Should().HaveCount(3);
        withConflict.Assignments[0].HasTimeConflict.Should().BeTrue();
        withConflict.Assignments[1].HasTimeConflict.Should().BeTrue();
        withConflict.Assignments[2].HasTimeConflict.Should().BeFalse();

        var withoutConflict = page.Items.Single(a => a.UserId == dani.Id);
        withoutConflict.Assignments.Should().HaveCount(2);
        withoutConflict.Assignments.Should().OnlyContain(a => !a.HasTimeConflict);
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SortByEmail_OrdersByEmailAscending()
    {
        var carla = NewUser("Carla", email: "charlie@test.local");
        var ana = NewUser("Ana", email: "alice@test.local");
        var berto = NewUser("Berto", email: "bob@test.local");
        HasConfirmedAttendees(carla, ana, berto);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Sort = "email" },
            TestContext.Current.CancellationToken
        );

        page.Items.Select(a => a.Email)
            .Should()
            .Equal("alice@test.local", "bob@test.local", "charlie@test.local");
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SortByBirthDateDescending_OrdersOldestLast()
    {
        var oldest = NewUser("Vieja", birthDate: new DateOnly(1980, 1, 1));
        var youngest = NewUser("Joven", birthDate: new DateOnly(2010, 1, 1));
        var middle = NewUser("Media", birthDate: new DateOnly(1995, 1, 1));
        HasConfirmedAttendees(oldest, youngest, middle);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Sort = "-birthDate" },
            TestContext.Current.CancellationToken
        );

        page.Items.Select(a => a.FirstName).Should().Equal("Joven", "Media", "Vieja");
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SortByType_OrdersByUserTypeName()
    {
        var volunteer = NewUser("Vero", typeName: "Voluntario");
        var member = NewUser("Mario", typeName: "Miembro");
        var sponsor = NewUser("Sonia", typeName: "Patrocinador");
        HasConfirmedAttendees(volunteer, member, sponsor);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Sort = "type" },
            TestContext.Current.CancellationToken
        );

        page.Items.Select(a => a.UserTypeName)
            .Should()
            .Equal("Miembro", "Patrocinador", "Voluntario");
    }

    [Fact]
    public async Task ListEventAttendeesAsync_EventMissing_ReturnsEmptyPage()
    {
        var ana = NewUser("Ana");
        HasConfirmedAttendees(ana);

        var page = await sut.ListEventAttendeesAsync(
            Guid.NewGuid(),
            new EventAttendeeListQuery(),
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListEventAttendeesAsync_SecondPage_ReturnsRemainingUsersWithTotal()
    {
        var ana = NewUser("Ana");
        var berto = NewUser("Berto");
        var carla = NewUser("Carla");
        HasConfirmedAttendees(carla, ana, berto);

        var page = await sut.ListEventAttendeesAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Page = 2, PageSize = 2 },
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(3);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.Items.Should().ContainSingle(a => a.UserId == carla.Id);
    }

    private static User BadgeUser(
        string first,
        string last,
        string typeName,
        string typeColor,
        DateTimeOffset createdAt,
        User? parent = null
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Phone = "600-" + first,
            CreatedAt = createdAt,
            Parent = parent,
            ParentId = parent?.Id,
            UserType = new UserType { Description = "Descripción de prueba", Name = typeName, Color = typeColor },
        };
    }

    private static ActivityUserRoleAssignment BadgeAsg(
        User user,
        string activityTitle,
        DateTimeOffset startsAt,
        Guid statusId,
        Guid? eventId = null
    )
    {
        return new()
        {
            UserId = user.Id,
            User = user,
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                Description = "Descripción de la actividad",
                Location = "Sala principal",
                EventId = eventId ?? QueriedEventId,
                Title = activityTitle,
                ActivityStartsAt = startsAt,
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task GetEventBadgesAsync_EventMissing_ReturnsNotFound()
    {
        HasEvents();

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
        var createdAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var parent = BadgeUser("Marta", "Miembro", "Socio", "#EF4444", createdAt);
        var child = BadgeUser("Mateo", "Miembro", "Participante", "#FFFFFF", createdAt, parent);
        var adult = BadgeUser("Ada", "Admin", "Socio", "#EF4444", createdAt);

        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Feria" });
        HasAssignments(
            BadgeAsg(adult, "Charla", When.AddHours(2), Confirmed),
            BadgeAsg(adult, "Taller", When, Confirmed),
            BadgeAsg(adult, "Taller", When, Confirmed),
            BadgeAsg(adult, "Otro evento", When, Confirmed, eventId: Guid.NewGuid()),
            BadgeAsg(child, "Taller infantil", When, Confirmed),
            BadgeAsg(child, "Cuentacuentos", When, Requested),
            BadgeAsg(parent, "Charla", When, Denied)
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
        childBadge.Guardian.FirstName.Should().Be("Marta");
        childBadge.Guardian.LastName.Should().Be("Miembro");
        childBadge.Guardian.Phone.Should().Be("600-Marta");
        childBadge.Activities.Should().Equal("Taller infantil");
    }

    [Fact]
    public async Task GetEventBadgesAsync_NoConfirmedAssignments_ReturnsEmptyBadges()
    {
        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Feria" });
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
    )
    {
        return new()
        {
            Description = "Descripción de la actividad",
            Id = Guid.NewGuid(),
            EventId = eventId ?? QueriedEventId,
            Title = title,
            Location = "Sala " + title,
            ActivityStartsAt = startsAt,
            ActivityEndsAt = startsAt.AddHours(1),
        };
    }

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
            ActivityRoleType = new ActivityRoleType { Description = "Descripción de prueba", Id = roleId, Name = roleName },
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task GetEventRosterAsync_EventMissing_ReturnsNotFound()
    {
        HasEvents();

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
        var taller = RosterActivity("Taller", When);
        var charla = RosterActivity("Charla", When.AddHours(2));
        var foreignActivity = RosterActivity("Ajena", When, eventId: Guid.NewGuid());

        var parent = NewUser("Marta");
        var child = NewUser("Zoe", parent);
        child.Email = null;
        child.Phone = null;
        child.BirthDate = new DateOnly(2016, 3, 2);
        var adult = NewUser("Ada");
        var requestedUser = NewUser("Rita");
        var deniedUser = NewUser("Dario");

        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Feria" });
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
        childRow.Guardian.FirstName.Should().Be("Marta");
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
        var taller = RosterActivity("Taller", When);

        var bruno = NewUser("Bruno");
        bruno.LastName = "Zeta";
        var ana = NewUser("Ana");
        ana.LastName = "Zeta";
        var zoe = NewUser("Zoe");
        zoe.LastName = "Alfa";
        var vera = NewUser("Vera");
        vera.LastName = "Alfa";

        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Feria" });
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
        HasEvents(new Event { Subtitle = "Subtítulo del evento", Id = QueriedEventId, Title = "Feria" });
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
        dashboard
            .GetCountsAsync(Arg.Any<CancellationToken>())
            .Returns(
                new DashboardCounts
                {
                    Events = 1,
                    Activities = 2,
                    Resources = 3,
                    Announcements = 4,
                    Partners = 5,
                    Users = 6,
                }
            );

        var result = await sut.GetDashboardSummaryAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new DashboardSummaryResponse(1, 2, 3, 4, 5, 6));
    }

    private void HasActivityRows(params Activity[] list)
    {
        activities.Query().Returns(list.AsQueryable());
    }

    private void HasResources(params Resource[] list)
    {
        resources.Query().Returns(list.AsQueryable());
    }

    private void HasAnnouncements(params Announcement[] list)
    {
        announcements.Query().Returns(list.AsQueryable());
    }

    private void HasPartners(params Partner[] list)
    {
        partners.Query().Returns(list.AsQueryable());
    }

    private void HasCategoryTypes(params EventCategoryType[] list)
    {
        eventCategoryTypes.Query().Returns(list.AsQueryable());
    }

    private static User AnalyticsUser(
        Guid typeId,
        Guid statusId,
        DateTimeOffset createdAt,
        Guid? parentId = null,
        Gender gender = Gender.Female
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = "U",
            LastName = "U",
            CreatedAt = createdAt,
            UserTypeId = typeId,
            UserStatusTypeId = statusId,
            ParentId = parentId,
            Gender = gender,
        };
    }

    private static ActivityUserRoleAssignment Insc(
        Guid eventId,
        Guid statusId,
        DateTimeOffset createdAt
    )
    {
        return new()
        {
            UserId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                Description = "Descripción de la actividad",
                Location = "Sala principal",
                Title = "Actividad de prueba",
                EventId = eventId,
            },
            AssignmentStatusId = statusId,
            CreatedAt = createdAt,
        };
    }

    private static Activity AnalyticsActivity(
        DateTimeOffset startsAt,
        DateTimeOffset createdAt,
        int desired,
        int confirmed,
        Guid eventId,
        string eventTitle = "Evento",
        string title = "Actividad"
    )
    {
        var activity = new Activity
        {
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            Id = Guid.NewGuid(),
            Title = title,
            ActivityStartsAt = startsAt,
            CreatedAt = createdAt,
            EventId = eventId,
            Event = new Event { Subtitle = "Subtítulo del evento", Id = eventId, Title = eventTitle },
        };
        if (desired > 0)
        {
            activity.RoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    DesiredCount = desired,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                }
            );
        }

        for (var i = 0; i < confirmed; i++)
        {
            activity.Assignments.Add(
                new ActivityUserRoleAssignment { AssignmentStatusId = Confirmed }
            );
        }

        return activity;
    }

    private static DashboardKpiResponse Kpi(DashboardAnalyticsResponse r, string key)
    {
        return r.Kpis.Single(k => string.Equals(k.Key, key, StringComparison.Ordinal));
    }

    private static int Slice(IReadOnlyList<DashboardSliceResponse> slices, string key)
    {
        return slices.Single(s => string.Equals(s.Key, key, StringComparison.Ordinal)).Count;
    }

    private static IReadOnlyList<int> Series(DashboardTimeSeriesResponse ts, string key)
    {
        return ts.Series.Single(s => string.Equals(s.Key, key, StringComparison.Ordinal)).Values;
    }

    [Fact]
    public async Task GetDashboardAnalyticsAsync_MixedData_ProducesExpectedSeriesAndBreakdowns()
    {
        var e1 = new Guid("eeeeeeee-0000-0000-0000-000000000001");
        var e2 = new Guid("eeeeeeee-0000-0000-0000-000000000002");

        var member = SeedIds.UserTypes.Member;
        var sponsor = SeedIds.UserTypes.Sponsor;
        var participant = SeedIds.UserTypes.Participant;
        var active = SeedIds.UserStatusTypes.Active;
        var dependent = SeedIds.UserStatusTypes.Dependent;

        var parent = AnalyticsUser(participant, active, Utc(2026, 6, 20), gender: Gender.Other);
        HasUsers(
            AnalyticsUser(member, active, Utc(2025, 12, 1)),
            AnalyticsUser(member, active, Utc(2026, 2, 15)),
            AnalyticsUser(sponsor, active, Utc(2026, 3, 10), gender: Gender.Male),
            parent,
            AnalyticsUser(participant, dependent, Utc(2026, 6, 25), parent.Id, Gender.Male)
        );

        HasAssignments(
            Insc(e1, Confirmed, Utc(2026, 2, 10)),
            Insc(e1, Confirmed, Utc(2026, 2, 20)),
            Insc(e1, Requested, Utc(2026, 3, 5)),
            Insc(e2, Denied, Utc(2026, 6, 1)),
            Insc(e2, Confirmed, Utc(2026, 6, 15))
        );

        HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = e1,
                Title = "Feria",
                CreatedAt = Utc(2026, 1, 5),
                EventStartsAt = new DateOnly(2026, 2, 1),
            },
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = e2,
                Title = "Taller",
                CreatedAt = Utc(2026, 5, 10),
                EventStartsAt = new DateOnly(2026, 9, 1),
            }
        );

        HasCategoryTypes(
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Formación",
                Color = "#F97316",
                Events = [new EventCategory(), new EventCategory()],
            },
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Robótica",
                Color = "#84CC16",
                Events = [new EventCategory()],
            }
        );

        HasResources(
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 3, 1),
                ResourceTypeId = SeedIds.ResourceTypes.Internal,
            },
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 4, 1),
                ResourceTypeId = SeedIds.ResourceTypes.External,
            },
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2025, 12, 15),
                ResourceTypeId = SeedIds.ResourceTypes.External,
            }
        );

        HasAnnouncements(
            new Announcement { Subtitle = "Subtítulo de la noticia", Title = "Noticia de prueba", CreatedAt = Utc(2026, 2, 1) },
            new Announcement { Subtitle = "Subtítulo de la noticia", Title = "Noticia de prueba", CreatedAt = Utc(2026, 5, 1) }
        );

        HasPartners(new Partner { Name = "Entidad colaboradora", CreatedAt = Utc(2026, 1, 10) });

        var occEventId = new Guid("cccccccc-0000-0000-0000-000000000001");
        HasActivityRows(
            AnalyticsActivity(
                Utc(2026, 9, 1),
                Utc(2026, 5, 1),
                desired: 5,
                confirmed: 3,
                eventId: occEventId,
                eventTitle: "Robótica",
                title: "Taller de septiembre"
            ),
            AnalyticsActivity(
                Utc(2026, 2, 1),
                Utc(2026, 1, 1),
                desired: 2,
                confirmed: 2,
                eventId: new Guid("cccccccc-0000-0000-0000-000000000002"),
                eventTitle: "Evento pasado"
            )
        );

        var r = await sut.GetDashboardAnalyticsAsync(
            new DashboardAnalyticsQuery
            {
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31),
            },
            TestContext.Current.CancellationToken
        );

        r.Granularity.Should().Be("month");
        r.UserGrowth.Buckets.Should().HaveCount(12);

        Kpi(r, "users").Should().BeEquivalentTo(new DashboardKpiResponse("users", 5, 4, 1));
        Kpi(r, "members").Should().BeEquivalentTo(new DashboardKpiResponse("members", 2, 1, 1));
        Kpi(r, "inscriptions")
            .Should()
            .BeEquivalentTo(new DashboardKpiResponse("inscriptions", 5, 5, 0));
        Kpi(r, "resources").Should().BeEquivalentTo(new DashboardKpiResponse("resources", 3, 2, 1));

        var memberSeries = Series(r.UserGrowth, "member");
        memberSeries[0].Should().Be(1);
        memberSeries[1].Should().Be(2);
        memberSeries[11].Should().Be(2);
        Series(r.UserGrowth, "sponsor")[2].Should().Be(1);
        Series(r.UserGrowth, "participant")[5].Should().Be(2);

        Series(r.Inscriptions, "confirmed")[1].Should().Be(2);
        Series(r.Inscriptions, "confirmed")[5].Should().Be(1);
        Series(r.Inscriptions, "requested")[2].Should().Be(1);

        Series(r.ContentPublished, "announcements")[1].Should().Be(1);
        Series(r.ContentPublished, "resources")[2].Should().Be(1);

        Slice(r.UsersByType, "member").Should().Be(2);
        Slice(r.UsersByType, "participant").Should().Be(2);
        Slice(r.AudienceComposition, "adults").Should().Be(4);
        Slice(r.AudienceComposition, "minors").Should().Be(1);
        Slice(r.ParticipantsByGender, "Male").Should().Be(1);
        Slice(r.ParticipantsByGender, "Other").Should().Be(1);
        Slice(r.ParticipantsByGender, "Female")
            .Should()
            .Be(0, "the two Female users are members, not participants");

        r.EventsByCategory.Select(c => c.Label).Should().Equal("Formación", "Robótica");
        r.EventsByCategory[0].Count.Should().Be(2);

        r.TopEvents.Select(t => t.Title).Should().Equal("Feria", "Taller");
        r.TopEvents[0].Confirmed.Should().Be(2);

        r.Occupancy.Confirmed.Should().Be(3);
        r.Occupancy.Desired.Should().Be(5);
        r.Occupancy.Events.Should().ContainSingle();
        var occEvent = r.Occupancy.Events[0];
        occEvent.EventId.Should().Be(occEventId);
        occEvent.Title.Should().Be("Robótica");
        occEvent.Confirmed.Should().Be(3);
        occEvent.Desired.Should().Be(5);
        occEvent.Activities.Should().ContainSingle();
        occEvent.Activities[0].Title.Should().Be("Taller de septiembre");
        occEvent.Activities[0].Confirmed.Should().Be(3);
        occEvent.Activities[0].Desired.Should().Be(5);
    }

    private static DateTimeOffset Utc(int year, int month, int day)
    {
        return new(year, month, day, 12, 0, 0, TimeSpan.Zero);
    }
}
