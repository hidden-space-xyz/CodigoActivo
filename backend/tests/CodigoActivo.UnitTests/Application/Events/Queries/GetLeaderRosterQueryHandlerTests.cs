using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetLeaderRosterQueryHandlerTests
{
    private static readonly Guid EventId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Leader = SeedIds.ActivityRoleTypes.Leader;
    private static readonly Guid Volunteer = SeedIds.ActivityRoleTypes.Volunteer;
    private static readonly Guid Participant = SeedIds.ActivityRoleTypes.Participant;
    private static readonly Guid Confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
    private static readonly Guid Requested = SeedIds.AssignmentStatusTypes.Requested;
    private static readonly Guid Denied = SeedIds.AssignmentStatusTypes.Denied;
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignedUp = new(2026, 6, 1, 9, 30, 0, TimeSpan.Zero);

    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly TestClock clock = new(Now, new DateOnly(2026, 7, 4));
    private readonly User caller = NewUser("Marta");
    private readonly GetLeaderRosterQueryHandler sut;

    public GetLeaderRosterQueryHandlerTests()
    {
        sut = new GetLeaderRosterQueryHandler(activities, new FakeQueryExecutor(), clock);
    }

    private static User NewUser(string firstName, User? guardian = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = firstName + " Apellido",
            Parent = guardian,
            ParentId = guardian?.Id,
        };
        if (guardian is null)
        {
            user.Email = firstName.ToLowerInvariant() + "@test.local";
            user.Phone = "600-" + firstName;
            user.SecondaryPhone = "699-" + firstName;
            user.NationalId = "DNI-" + firstName;
        }
        else
        {
            user.BirthDate = new DateOnly(2015, 5, 5);
        }

        return user;
    }

    private static Activity NewActivity(
        string title,
        DateTimeOffset? startsAt = null,
        Guid? eventId = null
    )
    {
        var start = startsAt ?? Now.AddDays(6);
        return new Activity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Descripción",
            Location = "Sala " + title,
            EventId = eventId ?? EventId,
            ActivityStartsAt = start,
            ActivityEndsAt = start.AddHours(2),
        };
    }

    private static ActivityUserRoleAssignment Asg(
        User user,
        Activity activity,
        Guid roleTypeId,
        Guid statusId,
        string? roleName = null
    )
    {
        return new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = roleTypeId,
            ActivityRoleType = new ActivityRoleType
            {
                Id = roleTypeId,
                Name = roleName ?? RoleName(roleTypeId),
                Description = "Descripción",
            },
            AssignmentStatusId = statusId,
            CreatedAt = SignedUp,
        };
    }

    private static string RoleName(Guid roleTypeId)
    {
        return roleTypeId == Leader ? "Líder"
            : roleTypeId == Volunteer ? "Voluntario"
            : "Participante";
    }

    private void HasAssignments(params ActivityUserRoleAssignment[] assignments)
    {
        activities.QueryAssignments().Returns(_ => assignments.AsQueryable());
    }

    private Task<IReadOnlyList<LeaderRosterActivityResponse>> HandleAsync(Guid? eventId = null)
    {
        return sut.HandleAsync(
            new GetLeaderRosterQuery(eventId ?? EventId, caller.Id),
            TestContext.Current.CancellationToken
        );
    }

    public static TheoryData<string, string> NonLeadingAssignments =>
        new()
        {
            { "Leader", "Requested" },
            { "Leader", "Denied" },
            { "Volunteer", "Confirmed" },
            { "Participant", "Confirmed" },
        };

    [Theory]
    [MemberData(nameof(NonLeadingAssignments))]
    public async Task HandleAsyncCallerWithoutConfirmedLeadershipReturnsEmpty(
        string role,
        string status
    )
    {
        var roleTypeId = role switch
        {
            "Leader" => Leader,
            "Volunteer" => Volunteer,
            _ => Participant,
        };
        var statusId = status switch
        {
            "Requested" => Requested,
            "Denied" => Denied,
            _ => Confirmed,
        };
        var activity = NewActivity("Taller");
        HasAssignments(
            Asg(caller, activity, roleTypeId, statusId),
            Asg(NewUser("Luis"), activity, Leader, Confirmed),
            Asg(NewUser("Ana"), activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        roster.Should().BeEmpty();
        activities.Received(1).QueryAssignments();
    }

    [Fact]
    public async Task HandleAsyncConfirmedLeaderGetsOnlyTheActivitiesTheyLeadInTheEvent()
    {
        var led = NewActivity("Taller");
        var sibling = NewActivity("Charla");
        var otherEvent = NewActivity("Ajena", eventId: Guid.NewGuid());
        var siblingAttendee = NewUser("Olga");
        HasAssignments(
            Asg(caller, led, Leader, Confirmed),
            Asg(caller, sibling, Participant, Confirmed),
            Asg(siblingAttendee, sibling, Participant, Confirmed),
            Asg(caller, otherEvent, Leader, Confirmed),
            Asg(siblingAttendee, otherEvent, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        var activity = roster.Should().ContainSingle().Subject;
        activity.ActivityId.Should().Be(led.Id);
        activity.Title.Should().Be("Taller");
        activity.Location.Should().Be("Sala Taller");
        activity.ActivityStartsAt.Should().Be(led.ActivityStartsAt);
        activity.ActivityEndsAt.Should().Be(led.ActivityEndsAt);
        activity.Roles.SelectMany(r => r.Users).Select(u => u.FirstName).Should().Equal("Marta");
    }

    [Fact]
    public async Task HandleAsyncActivityThatHasEndedIsNoLongerListed()
    {
        var ended = NewActivity("Terminada", Now.AddHours(-2));
        var running = NewActivity("En curso", Now.AddHours(-1));
        HasAssignments(
            Asg(caller, ended, Leader, Confirmed),
            Asg(caller, running, Leader, Confirmed)
        );

        var roster = await HandleAsync();

        roster.Select(a => a.ActivityId).Should().Equal(running.Id);
    }

    [Fact]
    public async Task HandleAsyncLedActivitiesAreOrderedByStartThenTitle()
    {
        var late = NewActivity("Alfa", Now.AddDays(2));
        var earlyB = NewActivity("Beta", Now.AddDays(1));
        var earlyA = NewActivity("Alfa", Now.AddDays(1));
        HasAssignments(
            Asg(caller, late, Leader, Confirmed),
            Asg(caller, earlyB, Leader, Confirmed),
            Asg(caller, earlyA, Leader, Confirmed)
        );

        var roster = await HandleAsync();

        roster.Select(a => a.ActivityId).Should().Equal(earlyA.Id, earlyB.Id, late.Id);
    }

    [Fact]
    public async Task HandleAsyncGroupsConfirmedAttendeesByRoleAndSplitsUsersFromDependents()
    {
        var activity = NewActivity("Taller");
        var guardian = NewUser("Gabriela");
        var beatriz = NewUser("Beatriz");
        var alvaro = NewUser("Álvaro");
        var child = NewUser("Nora", guardian);
        var volunteerChild = NewUser("Hugo", guardian);
        var coLeader = NewUser("Luis");
        var mentorRole = Guid.NewGuid();
        HasAssignments(
            Asg(beatriz, activity, Participant, Confirmed),
            Asg(child, activity, Participant, Confirmed),
            Asg(NewUser("Rita"), activity, Participant, Requested),
            Asg(NewUser("Diego"), activity, Volunteer, Denied),
            Asg(alvaro, activity, Participant, Confirmed),
            Asg(volunteerChild, activity, Volunteer, Confirmed),
            Asg(NewUser("Irene"), activity, mentorRole, Confirmed, "Mentor"),
            Asg(coLeader, activity, Leader, Confirmed),
            Asg(caller, activity, Leader, Confirmed)
        );

        var roster = await HandleAsync();

        var roles = roster.Should().ContainSingle().Subject.Roles;
        roles
            .Select(r => (r.RoleTypeId, r.RoleName))
            .Should()
            .Equal(
                (Leader, "Líder"),
                (Volunteer, "Voluntario"),
                (Participant, "Participante"),
                (mentorRole, "Mentor")
            );
        roles[0].Users.Select(u => u.FirstName).Should().Equal("Luis", "Marta");
        roles[0].Dependents.Should().BeEmpty();
        roles[1].Users.Should().BeEmpty();
        roles[1].Dependents.Select(d => d.FirstName).Should().Equal("Hugo");
        roles[2].Users.Select(u => u.FirstName).Should().Equal("Álvaro", "Beatriz");
        roles[2]
            .Users[1]
            .Should()
            .Be(
                new LeaderRosterUserResponse(
                    "Beatriz",
                    "Beatriz Apellido",
                    "beatriz@test.local",
                    "600-Beatriz",
                    SignedUp
                )
            );
        roles[2]
            .Dependents.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new LeaderRosterDependentResponse(
                    "Nora",
                    "Nora Apellido",
                    11,
                    new LeaderRosterGuardianResponse(
                        "Gabriela",
                        "Gabriela Apellido",
                        "gabriela@test.local",
                        "600-Gabriela"
                    ),
                    SignedUp
                )
            );
        roles[3].Users.Select(u => u.FirstName).Should().Equal("Irene");
    }

    [Fact]
    public async Task HandleAsyncDependentWithoutBirthDateHasNoAge()
    {
        var activity = NewActivity("Taller");
        var child = NewUser("Nora", NewUser("Gabriela"));
        child.BirthDate = null;
        HasAssignments(
            Asg(caller, activity, Leader, Confirmed),
            Asg(child, activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        roster[0].Roles[1].Dependents.Should().ContainSingle().Which.Age.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncDependentAgeIsTheAgeOnTheDayOfTheActivity()
    {
        var activity = NewActivity(
            "Taller",
            new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)
        );
        var child = NewUser("Nora", NewUser("Gabriela"));
        child.BirthDate = new DateOnly(2012, 7, 8);
        HasAssignments(
            Asg(caller, activity, Leader, Confirmed),
            Asg(child, activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        roster[0]
            .Roles[1]
            .Dependents.Should()
            .ContainSingle()
            .Which.Age.Should()
            .Be(14, "she is 13 today but turns 14 before the activity");
    }

    [Fact]
    public async Task HandleAsyncActivityDayFollowsTheConfiguredTimeZone()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        var activity = NewActivity(
            "Taller",
            new DateTimeOffset(2026, 7, 7, 23, 0, 0, TimeSpan.Zero)
        );
        var child = NewUser("Nora", NewUser("Gabriela"));
        child.BirthDate = new DateOnly(2012, 7, 8);
        HasAssignments(
            Asg(caller, activity, Leader, Confirmed),
            Asg(child, activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        roster[0]
            .Roles[1]
            .Dependents.Should()
            .ContainSingle()
            .Which.Age.Should()
            .Be(14, "the activity starts on 8 July in local time");
    }

    [Fact]
    public async Task HandleAsyncLeaderRequestBesideAnotherConfirmedRoleGrantsNothing()
    {
        var activity = NewActivity("Taller");
        HasAssignments(
            Asg(caller, activity, Leader, Requested),
            Asg(caller, activity, Participant, Confirmed),
            Asg(NewUser("Ana"), activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();

        roster.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncResponseSerializedCarriesNoDataBeyondTheAgreedFields()
    {
        var activity = NewActivity("Taller");
        var guardian = NewUser("Gabriela");
        var child = NewUser("Nora", guardian);
        var adult = NewUser("Beatriz");
        HasAssignments(
            Asg(caller, activity, Leader, Confirmed),
            Asg(adult, activity, Participant, Confirmed),
            Asg(child, activity, Participant, Confirmed)
        );

        var roster = await HandleAsync();
        var json = JsonSerializer.Serialize(roster);
        var lowerJson = json.ToLowerInvariant();

        json.Should().Contain("Beatriz");
        json.Should().NotContain(adult.Id.ToString());
        json.Should().NotContain(child.Id.ToString());
        json.Should().NotContain(caller.Id.ToString());
        json.Should().NotContain("DNI-");
        json.Should().NotContain("699-");
        json.Should().NotContain("2015-05-05");
        lowerJson.Should().NotContain("nationalid");
        lowerJson.Should().NotContain("birthdate");
        lowerJson.Should().NotContain("secondaryphone");
        lowerJson.Should().NotContain("userid");
        lowerJson.Should().NotContain("gender");
    }
}
