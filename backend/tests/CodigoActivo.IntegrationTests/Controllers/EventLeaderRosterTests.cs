using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventLeaderRosterTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid EventId = new("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid OtherEventId = new("eeeeeeee-0000-0000-0000-000000000002");
    private static readonly Guid LedActivityId = new("eeeeeeee-0000-0000-0000-000000000011");
    private static readonly Guid SiblingActivityId = new("eeeeeeee-0000-0000-0000-000000000012");
    private static readonly Guid OtherEventActivityId = new("eeeeeeee-0000-0000-0000-000000000013");
    private static readonly Guid ThumbnailId = new("eeeeeeee-0000-0000-0000-000000000021");

    private static readonly Guid CoLeaderId = new("eeeeeeee-0000-0000-0000-000000000031");
    private static readonly Guid AdultParticipantId = new("eeeeeeee-0000-0000-0000-000000000032");
    private static readonly Guid VolunteerId = new("eeeeeeee-0000-0000-0000-000000000033");
    private static readonly Guid GuardianId = new("eeeeeeee-0000-0000-0000-000000000034");
    private static readonly Guid GuardedChildId = new("eeeeeeee-0000-0000-0000-000000000035");
    private static readonly Guid RequestedId = new("eeeeeeee-0000-0000-0000-000000000036");
    private static readonly Guid DeniedId = new("eeeeeeee-0000-0000-0000-000000000037");
    private static readonly Guid SiblingOnlyId = new("eeeeeeee-0000-0000-0000-000000000038");

    private const string AdultParticipantNationalId = "12345678Z";
    private const string AdultParticipantSecondaryPhone = "+34699000099";

    private static readonly DateTimeOffset SeededAt = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LedStartsAt = new(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LedEndsAt = LedStartsAt.AddHours(2);

    private static User Adult(
        Guid id,
        string firstName,
        string lastName,
        string? secondaryPhone = null,
        string? nationalId = null
    )
    {
        return new User
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLowerInvariant()}@codigoactivo.test",
            Phone = $"+3460000{id.ToString()[^4..]}",
            SecondaryPhone = secondaryPhone,
            NationalId = nationalId ?? "87654321X",
            Gender = Gender.Female,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = SeededAt,
        };
    }

    private static ActivityUserRoleAssignment Assignment(
        Guid userId,
        Guid activityId,
        Guid roleTypeId,
        Guid statusId,
        int minutesAfterSeed = 0
    )
    {
        return new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = statusId,
            CreatedAt = SeededAt.AddMinutes(minutesAfterSeed),
        };
    }

    private static Event NewEvent(Guid id, string title)
    {
        return new Event
        {
            Id = id,
            Title = title,
            Subtitle = "Edición 2026",
            Description = "{}",
            EventStartsAt = new DateOnly(2026, 7, 10),
            EventEndsAt = new DateOnly(2026, 7, 11),
            SignupStartsAt = SeededAt,
            SignupEndsAt = SeededAt.AddDays(30),
            ThumbnailId = ThumbnailId,
            CreatedAt = SeededAt,
            CreatedBy = TestSeedData.Users.AdminId,
        };
    }

    private static Activity NewActivity(
        Guid id,
        Guid eventId,
        string title,
        DateTimeOffset startsAt
    )
    {
        return new Activity
        {
            Id = id,
            Title = title,
            Description = "Descripción",
            Location = "Aula 3",
            ActivityStartsAt = startsAt,
            ActivityEndsAt = startsAt.AddHours(2),
            EventId = eventId,
            ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
            ThumbnailId = ThumbnailId,
            CreatedAt = SeededAt,
            CreatedBy = TestSeedData.Users.AdminId,
        };
    }

    /// <summary>
    /// Seeds two events. The first one has the activity under test, whose attendees cover every
    /// role and status plus an adult and two dependents, and a sibling activity with its own
    /// confirmed attendee; the second event has another activity. The seeded member's own
    /// assignments are left to each test.
    /// </summary>
    private Task SeedEventsAsync(params ActivityUserRoleAssignment[] memberAssignments)
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = ThumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Events.AddRange(
                NewEvent(EventId, "Jornada tecnológica"),
                NewEvent(OtherEventId, "Otra jornada")
            );
            db.Activities.AddRange(
                NewActivity(LedActivityId, EventId, "Taller de robótica", LedStartsAt),
                NewActivity(SiblingActivityId, EventId, "Taller de cocina", LedStartsAt),
                NewActivity(OtherEventActivityId, OtherEventId, "Taller de radio", LedStartsAt)
            );
            db.Users.AddRange(
                Adult(CoLeaderId, "Luis", "Lozano"),
                Adult(
                    AdultParticipantId,
                    "Ana",
                    "Álvarez",
                    AdultParticipantSecondaryPhone,
                    AdultParticipantNationalId
                ),
                Adult(VolunteerId, "Víctor", "Vega"),
                Adult(GuardianId, "Gabriela", "Gil"),
                Adult(RequestedId, "Rita", "Ruiz"),
                Adult(DeniedId, "Diego", "Díaz"),
                Adult(SiblingOnlyId, "Olga", "Ortiz"),
                new User
                {
                    Id = GuardedChildId,
                    FirstName = "Nora",
                    LastName = "Gil",
                    BirthDate = new DateOnly(2012, 7, 5),
                    Gender = Gender.Female,
                    ParentId = GuardianId,
                    UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                    UserTypeId = SeedIds.UserTypes.Participant,
                    CreatedAt = SeededAt,
                }
            );
            db.ActivityUserRoleAssignments.AddRange(
                Assignment(
                    CoLeaderId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Leader,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    1
                ),
                Assignment(
                    AdultParticipantId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    2
                ),
                Assignment(
                    VolunteerId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Volunteer,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    3
                ),
                Assignment(
                    GuardedChildId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    4
                ),
                Assignment(
                    TestSeedData.Users.MemberChildId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    5
                ),
                Assignment(
                    RequestedId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Requested,
                    6
                ),
                Assignment(
                    DeniedId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Volunteer,
                    SeedIds.AssignmentStatusTypes.Denied,
                    7
                ),
                Assignment(
                    SiblingOnlyId,
                    SiblingActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    8
                ),
                Assignment(
                    SiblingOnlyId,
                    OtherEventActivityId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    9
                )
            );
            db.ActivityUserRoleAssignments.AddRange(memberAssignments);
            return Task.CompletedTask;
        });
    }

    private static ActivityUserRoleAssignment MemberAssignment(
        Guid activityId,
        Guid roleTypeId,
        Guid statusId
    )
    {
        return Assignment(TestSeedData.Users.MemberId, activityId, roleTypeId, statusId);
    }

    private static ActivityUserRoleAssignment MemberLeadsTheActivity()
    {
        return MemberAssignment(
            LedActivityId,
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
    }

    private static async Task<List<LeaderRosterActivityResponse>> GetRosterAsync(
        HttpClient client,
        Guid eventId
    )
    {
        using var response = await client.GetAsync(
            TestUri.Rel($"/api/events/{eventId}/leader-roster"),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.ReadJsonAsync<List<LeaderRosterActivityResponse>>(Ct))!;
    }

    [Fact]
    public async Task LeaderRosterAnonymousReturnsUnauthorized()
    {
        await SeedEventsAsync(MemberLeadsTheActivity());
        var client = CreateClient();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/events/{EventId}/leader-roster"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaderRosterConfirmedLeaderGetsOnlyTheLedActivityWithConfirmedAttendees()
    {
        await SeedEventsAsync(
            MemberLeadsTheActivity(),
            MemberAssignment(
                SiblingActivityId,
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
            MemberAssignment(
                OtherEventActivityId,
                SeedIds.ActivityRoleTypes.Leader,
                SeedIds.AssignmentStatusTypes.Confirmed
            )
        );
        var client = await LoginAsMemberAsync();

        var roster = await GetRosterAsync(client, EventId);

        var activity = roster.Should().ContainSingle().Subject;
        activity.ActivityId.Should().Be(LedActivityId);
        activity.Title.Should().Be("Taller de robótica");
        activity.Location.Should().Be("Aula 3");
        activity.ActivityStartsAt.Should().Be(LedStartsAt);
        activity.ActivityEndsAt.Should().Be(LedEndsAt);
        activity
            .Roles.Select(r => r.RoleTypeId)
            .Should()
            .Equal(
                SeedIds.ActivityRoleTypes.Leader,
                SeedIds.ActivityRoleTypes.Volunteer,
                SeedIds.ActivityRoleTypes.Participant
            );

        var leaders = activity.Roles[0];
        leaders.Users.Select(u => u.FirstName).Should().Equal("Luis", "Marta");
        leaders.Dependents.Should().BeEmpty();
        var self = leaders.Users[1];
        self.LastName.Should().Be("Miembro");
        self.Email.Should().Be(TestSeedData.MemberEmail);
        self.Phone.Should().Be("+34600000002");

        var volunteers = activity.Roles[1];
        volunteers.Users.Select(u => u.FirstName).Should().Equal("Víctor");
        volunteers.Dependents.Should().BeEmpty();

        var participants = activity.Roles[2];
        var adult = participants.Users.Should().ContainSingle().Subject;
        adult.FirstName.Should().Be("Ana");
        adult.LastName.Should().Be("Álvarez");
        adult.Email.Should().Be("ana@codigoactivo.test");
        adult.Phone.Should().Be("+3460000" + AdultParticipantId.ToString()[^4..]);
        adult.SignedUpAt.Should().Be(SeededAt.AddMinutes(2));

        participants.Dependents.Select(d => d.FirstName).Should().Equal("Mateo", "Nora");
        var mateo = participants.Dependents[0];
        mateo.LastName.Should().Be("Miembro");
        mateo.Age.Should().Be(11);
        mateo
            .Guardian.Should()
            .Be(
                new LeaderRosterGuardianResponse(
                    "Marta",
                    "Miembro",
                    TestSeedData.MemberEmail,
                    "+34600000002"
                )
            );
        mateo.SignedUpAt.Should().Be(SeededAt.AddMinutes(5));
        var nora = participants.Dependents[1];
        nora.Age.Should().Be(14, "she is 13 today but turns 14 before the activity");
        nora.Guardian.FirstName.Should().Be("Gabriela");
        nora.Guardian.Email.Should().Be("gabriela@codigoactivo.test");
    }

    [Fact]
    public async Task LeaderRosterResponseNeverCarriesDataOutsideTheAgreedFields()
    {
        await SeedEventsAsync(MemberLeadsTheActivity());
        var client = await LoginAsMemberAsync();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/events/{EventId}/leader-roster"),
            Ct
        );
        var json = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("Ana");
        json.Should().NotContain(AdultParticipantNationalId);
        json.Should().NotContain(TestSeedData.MemberNationalId);
        json.Should().NotContain(AdultParticipantSecondaryPhone);
        json.Should().NotContain("2012-07-05");
        json.Should().NotContain("2015-05-05");
        json.Should().NotContain(AdultParticipantId.ToString());
        json.Should().NotContain(TestSeedData.Users.MemberChildId.ToString());
        json.Should().NotContain("Rita", "requested signups are not confirmed attendees");
        json.Should().NotContain("Diego", "denied signups are not confirmed attendees");
        json.Should().NotContain("Olga", "she only attends activities the member does not lead");
        var lowerJson = json.ToLowerInvariant();
        lowerJson.Should().NotContain("nationalid");
        lowerJson.Should().NotContain("birthdate");
        lowerJson.Should().NotContain("secondaryphone");
        lowerJson.Should().NotContain("userid");
        lowerJson.Should().NotContain("gender");
    }

    [Theory]
    [InlineData("Requested")]
    [InlineData("Denied")]
    public async Task LeaderRosterUnconfirmedLeaderGetsNothing(string status)
    {
        var statusId =
            status == "Requested"
                ? SeedIds.AssignmentStatusTypes.Requested
                : SeedIds.AssignmentStatusTypes.Denied;
        await SeedEventsAsync(
            MemberAssignment(LedActivityId, SeedIds.ActivityRoleTypes.Leader, statusId)
        );
        var client = await LoginAsMemberAsync();

        var roster = await GetRosterAsync(client, EventId);

        roster.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Volunteer")]
    [InlineData("Participant")]
    public async Task LeaderRosterConfirmedAttendeeWithoutLeaderRoleGetsNothing(string role)
    {
        var roleTypeId =
            role == "Volunteer"
                ? SeedIds.ActivityRoleTypes.Volunteer
                : SeedIds.ActivityRoleTypes.Participant;
        await SeedEventsAsync(
            MemberAssignment(LedActivityId, roleTypeId, SeedIds.AssignmentStatusTypes.Confirmed)
        );
        var client = await LoginAsMemberAsync();

        var roster = await GetRosterAsync(client, EventId);

        roster.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaderRosterStaysAvailableUntilTheActivityEnds()
    {
        await SeedEventsAsync(MemberLeadsTheActivity());
        var client = await LoginAsMemberAsync();

        Factory.Clock.UtcNow = LedEndsAt.AddMinutes(-1);
        var beforeEnd = await GetRosterAsync(client, EventId);
        Factory.Clock.UtcNow = LedEndsAt;
        var atEnd = await GetRosterAsync(client, EventId);

        beforeEnd.Should().ContainSingle(a => a.ActivityId == LedActivityId);
        atEnd.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaderRosterStopsAsSoonAsTheLeadershipIsNoLongerConfirmed()
    {
        await SeedEventsAsync(MemberLeadsTheActivity());
        var client = await LoginAsMemberAsync();
        var whileConfirmed = await GetRosterAsync(client, EventId);

        await Factory.SeedAsync(db =>
            db.ActivityUserRoleAssignments.Where(a =>
                    a.UserId == TestSeedData.Users.MemberId && a.ActivityId == LedActivityId
                )
                .ExecuteUpdateAsync(
                    set =>
                        set.SetProperty(
                            a => a.AssignmentStatusId,
                            SeedIds.AssignmentStatusTypes.Denied
                        ),
                    Ct
                )
        );
        var afterDenial = await GetRosterAsync(client, EventId);

        whileConfirmed.Should().ContainSingle();
        afterDenial.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaderRosterAdministratorWhoLeadsNothingGetsNothing()
    {
        await SeedEventsAsync(MemberLeadsTheActivity());
        var client = await LoginAsAdminAsync();

        var roster = await GetRosterAsync(client, EventId);

        roster.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaderRosterAdministratorWhoLeadsGetsOnlyTheLedActivity()
    {
        await SeedEventsAsync(
            Assignment(
                TestSeedData.Users.AdminId,
                SiblingActivityId,
                SeedIds.ActivityRoleTypes.Leader,
                SeedIds.AssignmentStatusTypes.Confirmed
            )
        );
        var client = await LoginAsAdminAsync();

        var roster = await GetRosterAsync(client, EventId);

        var activity = roster.Should().ContainSingle().Subject;
        activity.ActivityId.Should().Be(SiblingActivityId);
        activity
            .Roles.SelectMany(r => r.Users)
            .Select(u => u.FirstName)
            .Should()
            .BeEquivalentTo("Ada", "Olga");
    }

    [Fact]
    public async Task LeaderRosterGuardianOfALeadingDependentGetsNothing()
    {
        await SeedEventsAsync();
        await Factory.SeedAsync(async db =>
        {
            await db
                .ActivityUserRoleAssignments.Where(a =>
                    a.UserId == TestSeedData.Users.MemberChildId && a.ActivityId == LedActivityId
                )
                .ExecuteDeleteAsync(Ct);
            db.ActivityUserRoleAssignments.Add(
                Assignment(
                    TestSeedData.Users.MemberChildId,
                    LedActivityId,
                    SeedIds.ActivityRoleTypes.Leader,
                    SeedIds.AssignmentStatusTypes.Confirmed
                )
            );
        });
        var client = await LoginAsMemberAsync();

        var roster = await GetRosterAsync(client, EventId);

        roster.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaderRosterIsScopedToTheRequestedEvent()
    {
        await SeedEventsAsync(
            MemberAssignment(
                OtherEventActivityId,
                SeedIds.ActivityRoleTypes.Leader,
                SeedIds.AssignmentStatusTypes.Confirmed
            )
        );
        var client = await LoginAsMemberAsync();

        var requestedEvent = await GetRosterAsync(client, EventId);
        var ledEvent = await GetRosterAsync(client, OtherEventId);
        var unknownEvent = await GetRosterAsync(client, Guid.NewGuid());

        requestedEvent.Should().BeEmpty();
        var activity = ledEvent.Should().ContainSingle().Subject;
        activity.ActivityId.Should().Be(OtherEventActivityId);
        activity
            .Roles.SelectMany(r => r.Users)
            .Select(u => u.FirstName)
            .Should()
            .BeEquivalentTo("Marta", "Olga");
        unknownEvent.Should().BeEmpty();
    }
}
