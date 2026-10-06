using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
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
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}@codigoactivo.test",
                Phone = $"+3460000{id.ToString()[^4..]}",
                SecondaryPhone = secondaryPhone,
                NationalId = nationalId ?? "87654321X",
                Gender = Gender.Female,
                Status = UserStatus.Active,
                UserType = UserType.Participant,
                CreatedAt = SeededAt,
            }
        );
    }

    private static Assignment NewAssignment(
        Guid userId,
        Guid activityId,
        Guid roleTypeId,
        Guid statusId,
        int minutesAfterSeed = 0
    )
    {
        return Persisted.As<Assignment>(
            new
            {
                UserId = userId,
                ActivityId = activityId,
                Role = CatalogIds.ActivityRoles.ValueOf(roleTypeId),
                Status = CatalogIds.AssignmentStatuses.ValueOf(statusId),
                CreatedAt = SeededAt.AddMinutes(minutesAfterSeed),
            }
        );
    }

    private static Event NewEvent(Guid id, string title)
    {
        return Persisted.As<Event>(
            new
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
            }
        );
    }

    private static Activity NewActivity(
        Guid id,
        Guid eventId,
        string title,
        DateTimeOffset startsAt
    )
    {
        return Persisted.As<Activity>(
            new
            {
                Id = id,
                Title = title,
                Description = "Descripción",
                Location = "Aula 3",
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt.AddHours(2),
                EventId = eventId,
                Modality = ActivityModality.Presencial,
                ThumbnailId = ThumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    /// <summary>
    /// Seeds two events. The first one has the activity under test, whose attendees cover every
    /// role and status plus an adult and two dependents, and a sibling activity with its own
    /// confirmed attendee; the second event has another activity. The seeded member's own
    /// assignments are left to each test.
    /// </summary>
    private Task SeedEventsAsync(params Assignment[] memberAssignments)
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.Add(
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = ThumbnailId,
                        Name = "thumb",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = TestSeedData.Users.AdminId,
                    }
                )
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
                Persisted.As<User>(
                    new
                    {
                        Id = GuardedChildId,
                        FirstName = "Nora",
                        LastName = "Gil",
                        BirthDate = new DateOnly(2012, 7, 5),
                        Gender = Gender.Female,
                        ParentId = GuardianId,
                        Status = UserStatus.Dependent,
                        UserType = UserType.Participant,
                        CreatedAt = SeededAt,
                    }
                )
            );
            db.Assignments.AddRange(
                NewAssignment(
                    CoLeaderId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Leader,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    1
                ),
                NewAssignment(
                    AdultParticipantId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    2
                ),
                NewAssignment(
                    VolunteerId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Volunteer,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    3
                ),
                NewAssignment(
                    GuardedChildId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    4
                ),
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    5
                ),
                NewAssignment(
                    RequestedId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Requested,
                    6
                ),
                NewAssignment(
                    DeniedId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Volunteer,
                    KnownIds.AssignmentStatusTypes.Denied,
                    7
                ),
                NewAssignment(
                    SiblingOnlyId,
                    SiblingActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    8
                ),
                NewAssignment(
                    SiblingOnlyId,
                    OtherEventActivityId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    9
                )
            );
            db.Assignments.AddRange(memberAssignments);
            return Task.CompletedTask;
        });
    }

    private static Assignment MemberAssignment(Guid activityId, Guid roleTypeId, Guid statusId)
    {
        return NewAssignment(TestSeedData.Users.MemberId, activityId, roleTypeId, statusId);
    }

    private static Assignment MemberLeadsTheActivity()
    {
        return MemberAssignment(
            LedActivityId,
            KnownIds.ActivityRoleTypes.Leader,
            KnownIds.AssignmentStatusTypes.Confirmed
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
                KnownIds.ActivityRoleTypes.Participant,
                KnownIds.AssignmentStatusTypes.Confirmed
            ),
            MemberAssignment(
                OtherEventActivityId,
                KnownIds.ActivityRoleTypes.Leader,
                KnownIds.AssignmentStatusTypes.Confirmed
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
                KnownIds.ActivityRoleTypes.Leader,
                KnownIds.ActivityRoleTypes.Volunteer,
                KnownIds.ActivityRoleTypes.Participant
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
                ? KnownIds.AssignmentStatusTypes.Requested
                : KnownIds.AssignmentStatusTypes.Denied;
        await SeedEventsAsync(
            MemberAssignment(LedActivityId, KnownIds.ActivityRoleTypes.Leader, statusId)
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
                ? KnownIds.ActivityRoleTypes.Volunteer
                : KnownIds.ActivityRoleTypes.Participant;
        await SeedEventsAsync(
            MemberAssignment(LedActivityId, roleTypeId, KnownIds.AssignmentStatusTypes.Confirmed)
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
            db.Assignments.Where(a =>
                    a.UserId == UserId.From(TestSeedData.Users.MemberId)
                    && a.ActivityId == ActivityId.From(LedActivityId)
                )
                .ExecuteUpdateAsync(
                    set => set.SetProperty(a => a.Status, AssignmentStatus.Denied),
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
            NewAssignment(
                TestSeedData.Users.AdminId,
                SiblingActivityId,
                KnownIds.ActivityRoleTypes.Leader,
                KnownIds.AssignmentStatusTypes.Confirmed
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
                .Assignments.Where(a =>
                    a.UserId == UserId.From(TestSeedData.Users.MemberChildId)
                    && a.ActivityId == ActivityId.From(LedActivityId)
                )
                .ExecuteDeleteAsync(Ct);
            db.Assignments.Add(
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    LedActivityId,
                    KnownIds.ActivityRoleTypes.Leader,
                    KnownIds.AssignmentStatusTypes.Confirmed
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
                KnownIds.ActivityRoleTypes.Leader,
                KnownIds.AssignmentStatusTypes.Confirmed
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
