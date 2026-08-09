using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class ListEventAttendeesQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ListEventAttendeesQueryHandler sut;

    public ListEventAttendeesQueryHandlerTests()
    {
        sut = new ListEventAttendeesQueryHandler(users, new FakeQueryExecutor());
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
            ActivityRoleType = new ActivityRoleType
            {
                Description = "Descripción de prueba",
                Id = AlphaRoleId,
                Name = "Alpha",
            },
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

        users.HasUsers(attendees);
    }

    private Task<PagedResult<EventAttendeeResponse>> ListAsync(
        Guid eventId,
        EventAttendeeListQuery filters
    )
    {
        return sut.HandleAsync(
            new ListEventAttendeesQuery(eventId, filters),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAssignmentsAcrossActivitiesGroupsPerUserWithOrderedAssignments()
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
        users.HasUsers(berto, ana, outsider);

        var page = await ListAsync(QueriedEventId, new EventAttendeeListQuery());

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
    public async Task HandleAsyncSearchMatchingGuardianNameFoldsAccentsAndFiltersUsers()
    {
        var zoe = NewUser("Zoe", NewUser("María"));
        var berto = NewUser("Berto");
        HasConfirmedAttendees(zoe, berto);

        var page = await ListAsync(QueriedEventId, new EventAttendeeListQuery { Search = "MARIA" });

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(zoe.Id);
    }

    [Fact]
    public async Task HandleAsyncSearchMatchingOwnPhoneFiltersUsers()
    {
        var zoe = NewUser("Zoe", NewUser("María"));
        var berto = NewUser("Berto");
        HasConfirmedAttendees(zoe, berto);

        var page = await ListAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Search = "555-berto" }
        );

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(berto.Id);
    }

    [Fact]
    public async Task HandleAsyncActivityAndStatusFiltersRequireOneAssignmentMatchingBoth()
    {
        var activityA = Guid.NewGuid();
        var activityB = Guid.NewGuid();
        var carla = NewUser("Carla");
        var dani = NewUser("Dani");
        Enroll(carla, "Taller A", When, Confirmed, "Confirmada", activityId: activityA);
        Enroll(carla, "Taller B", When.AddHours(1), Confirmed, "Confirmada", activityId: activityB);
        Enroll(dani, "Taller A", When, Requested, "Solicitada", activityId: activityA);
        Enroll(dani, "Taller B", When.AddHours(1), Confirmed, "Confirmada", activityId: activityB);
        users.HasUsers(carla, dani);

        var page = await ListAsync(
            QueriedEventId,
            new EventAttendeeListQuery { ActivityId = activityA, StatusId = Confirmed }
        );

        page.Total.Should().Be(1);
        var attendee = page.Items.Single();
        attendee.UserId.Should().Be(carla.Id);
        attendee.Assignments.Should().HaveCount(1);
        attendee.Assignments[0].ActivityId.Should().Be(activityA);
        attendee.Assignments[0].HasTimeConflict.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncUserTypeFilterReturnsOnlyMatchingUsers()
    {
        var ana = NewUser("Ana", userTypeId: SeedIds.UserTypes.Participant);
        var berto = NewUser("Berto");
        HasConfirmedAttendees(ana, berto);

        var page = await ListAsync(
            QueriedEventId,
            new EventAttendeeListQuery { UserTypeId = SeedIds.UserTypes.Participant }
        );

        page.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(ana.Id);
    }

    [Fact]
    public async Task HandleAsyncOverlappingAssignmentsFlagsConflictsExcludingDenied()
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
        users.HasUsers(carla, dani);

        var page = await ListAsync(QueriedEventId, new EventAttendeeListQuery());

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
    public async Task HandleAsyncSortByEmailOrdersByEmailAscending()
    {
        var carla = NewUser("Carla", email: "charlie@test.local");
        var ana = NewUser("Ana", email: "alice@test.local");
        var berto = NewUser("Berto", email: "bob@test.local");
        HasConfirmedAttendees(carla, ana, berto);

        var page = await ListAsync(QueriedEventId, new EventAttendeeListQuery { Sort = "email" });

        page.Items.Select(a => a.Email)
            .Should()
            .Equal("alice@test.local", "bob@test.local", "charlie@test.local");
    }

    [Fact]
    public async Task HandleAsyncSortByBirthDateDescendingOrdersOldestLast()
    {
        var oldest = NewUser("Vieja", birthDate: new DateOnly(1980, 1, 1));
        var youngest = NewUser("Joven", birthDate: new DateOnly(2010, 1, 1));
        var middle = NewUser("Media", birthDate: new DateOnly(1995, 1, 1));
        HasConfirmedAttendees(oldest, youngest, middle);

        var page = await ListAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Sort = "-birthDate" }
        );

        page.Items.Select(a => a.FirstName).Should().Equal("Joven", "Media", "Vieja");
    }

    [Fact]
    public async Task HandleAsyncSortByTypeOrdersByUserTypeName()
    {
        var volunteer = NewUser("Vero", typeName: "Voluntario");
        var member = NewUser("Mario", typeName: "Miembro");
        var sponsor = NewUser("Sonia", typeName: "Patrocinador");
        HasConfirmedAttendees(volunteer, member, sponsor);

        var page = await ListAsync(QueriedEventId, new EventAttendeeListQuery { Sort = "type" });

        page.Items.Select(a => a.UserTypeName)
            .Should()
            .Equal("Miembro", "Patrocinador", "Voluntario");
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsEmptyPage()
    {
        var ana = NewUser("Ana");
        HasConfirmedAttendees(ana);

        var page = await ListAsync(Guid.NewGuid(), new EventAttendeeListQuery());

        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncSecondPageReturnsRemainingUsersWithTotal()
    {
        var ana = NewUser("Ana");
        var berto = NewUser("Berto");
        var carla = NewUser("Carla");
        HasConfirmedAttendees(carla, ana, berto);

        var page = await ListAsync(
            QueriedEventId,
            new EventAttendeeListQuery { Page = 2, PageSize = 2 }
        );

        page.Total.Should().Be(3);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.Items.Should().ContainSingle(a => a.UserId == carla.Id);
    }
}
