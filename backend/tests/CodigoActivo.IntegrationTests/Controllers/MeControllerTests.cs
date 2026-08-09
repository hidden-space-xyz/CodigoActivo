using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class MeControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FileEntity Thumbnail(Guid id)
    {
        return new()
        {
            Id = id,
            Name = "thumb",
            Extension = "png",
            UploadedAt = SeededAt,
            UploadedBy = TestSeedData.Users.AdminId,
        };
    }

    private async Task<Guid> SeedAssignmentAsync(
        Guid userId,
        string activityTitle,
        DateTimeOffset activityStartsAt,
        Guid roleTypeId,
        Guid statusId,
        DateOnly? eventStartsAt = null,
        DateOnly? eventEndsAt = null
    )
    {
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var eventThumbnailId = Guid.NewGuid();
        var activityThumbnailId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.AddRange(Thumbnail(eventThumbnailId), Thumbnail(activityThumbnailId));
            db.Events.Add(
                new Event
                {
                    Id = eventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = eventStartsAt ?? new DateOnly(2026, 2, 1),
                    EventEndsAt = eventEndsAt ?? new DateOnly(2026, 2, 2),
                    SignupStartsAt = SeededAt,
                    SignupEndsAt = SeededAt.AddDays(10),
                    ThumbnailId = eventThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = activityId,
                    Title = activityTitle,
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = activityStartsAt,
                    ActivityEndsAt = activityStartsAt.AddHours(2),
                    EventId = eventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = activityThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.ActivityUserRoleAssignments.Add(
                new ActivityUserRoleAssignment
                {
                    UserId = userId,
                    ActivityId = activityId,
                    ActivityRoleTypeId = roleTypeId,
                    AssignmentStatusId = statusId,
                }
            );
            return Task.CompletedTask;
        });
        return eventId;
    }

    private static async Task<List<Application.DTOs.AssignedActivityResponse>> ReadAssignedAsync(
        HttpResponseMessage response
    )
    {
        return await response.ReadJsonAsync<List<Application.DTOs.AssignedActivityResponse>>(Ct)
            ?? [];
    }

    [Fact]
    public async Task AssignedActivitiesAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/me/assigned-activities"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignedActivitiesMemberWithAssignmentReturnsProjectedAssignment()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller de robótica",
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/me/assigned-activities"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        var assigned = items.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Taller de robótica");
        assigned.Description.Should().Be("Descripción");
        assigned.RoleType.Id.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        assigned.RoleType.Name.Should().Be("Líder");
        assigned.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        assigned.Status.Name.Should().Be("Confirmada");
    }

    [Fact]
    public async Task AssignedActivitiesEventIdFilterExcludesOtherEventAssignments()
    {
        var targetEventId = await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Dentro del evento",
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested
        );
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Fuera del evento",
            new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/me/assigned-activities?eventId={targetEventId}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        var assigned = items.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Dentro del evento");
        assigned.EventId.Should().Be(targetEventId);
    }

    private static readonly DateOnly PastStart = new(2026, 6, 1);
    private static readonly DateOnly PastEnd = new(2026, 6, 2);
    private static readonly DateOnly TodayStart = new(2026, 7, 3);
    private static readonly DateOnly TodayEnd = new(2026, 7, 4);
    private static readonly DateOnly FutureStart = new(2026, 8, 1);
    private static readonly DateOnly FutureEnd = new(2026, 8, 2);

    private static async Task<List<EventHistoryResponse>> ReadHistoryAsync(
        HttpResponseMessage response
    )
    {
        return await response.ReadJsonAsync<List<EventHistoryResponse>>(Ct) ?? [];
    }

    private async Task<List<EventHistoryResponse>> GetHistoryAsMemberAsync()
    {
        var client = await LoginAsMemberAsync();
        var response = await client.GetAsync(TestUri.Rel("/api/me/event-history"), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadHistoryAsync(response);
    }

    [Fact]
    public async Task EventHistoryAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/me/event-history"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EventHistoryPastEventWithoutConfirmedAssignmentOmitsEvent()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller pasado",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested,
            PastStart,
            PastEnd
        );

        var history = await GetHistoryAsMemberAsync();

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task EventHistoryPastEventWithConfirmedAssignmentIncludesEventMarkedAsPast()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller pasado",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            PastStart,
            PastEnd
        );

        var history = await GetHistoryAsMemberAsync();

        var entry = history.Should().ContainSingle().Subject;
        entry.IsPast.Should().BeTrue();
        entry.CanRate.Should().BeTrue();
        entry.MyRating.Should().BeNull();
        entry.Activities.Should().ContainSingle().Which.Title.Should().Be("Taller pasado");
    }

    [Fact]
    public async Task EventHistoryFutureEventWithRequestedAssignmentKeepsAssignmentAndStatus()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller futuro",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Volunteer,
            SeedIds.AssignmentStatusTypes.Requested,
            FutureStart,
            FutureEnd
        );

        var history = await GetHistoryAsMemberAsync();

        var entry = history.Should().ContainSingle().Subject;
        entry.IsPast.Should().BeFalse();
        entry.CanRate.Should().BeFalse();
        var activity = entry.Activities.Should().ContainSingle().Subject;
        activity.StatusId.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        activity.StatusName.Should().Be("Solicitada");
        activity.RoleTypeName.Should().Be("Voluntario");
    }

    [Fact]
    public async Task EventHistoryChildAssignmentIncludesHouseholdMemberAsNotSelf()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberChildId,
            "Taller del menor",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested,
            FutureStart,
            FutureEnd
        );

        var history = await GetHistoryAsMemberAsync();

        var entry = history.Should().ContainSingle().Subject;
        var activity = entry.Activities.Should().ContainSingle().Subject;
        activity.UserId.Should().Be(TestSeedData.Users.MemberChildId);
        activity.IsSelf.Should().BeFalse();
        activity.FirstName.Should().Be("Mateo");
    }

    [Fact]
    public async Task EventHistoryUpcomingAndPastEventsOrdersUpcomingFirst()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller pasado",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            PastStart,
            PastEnd
        );
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller futuro",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            FutureStart,
            FutureEnd
        );

        var history = await GetHistoryAsMemberAsync();

        history.Should().HaveCount(2);
        history[0].IsPast.Should().BeFalse();
        history[1].IsPast.Should().BeTrue();
    }

    [Fact]
    public async Task EventHistoryOtherUsersAssignmentIsNotReturned()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.AdminId,
            "Taller ajeno",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed,
            FutureStart,
            FutureEnd
        );

        var history = await GetHistoryAsMemberAsync();

        history.Should().BeEmpty();
    }

    private async Task SeedExtraAssignmentAsync(
        Guid eventId,
        Guid userId,
        string activityTitle,
        Guid statusId
    )
    {
        var activityId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(Thumbnail(thumbnailId));
            db.Activities.Add(
                new Activity
                {
                    Id = activityId,
                    Title = activityTitle,
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                    ActivityEndsAt = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                    EventId = eventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.ActivityUserRoleAssignments.Add(
                new ActivityUserRoleAssignment
                {
                    UserId = userId,
                    ActivityId = activityId,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                    AssignmentStatusId = statusId,
                }
            );
            return Task.CompletedTask;
        });
    }

    private async Task<List<EventCertificateResponse>> GetCertificatesAsMemberAsync()
    {
        var client = await LoginAsMemberAsync();
        var response = await client.GetAsync(TestUri.Rel("/api/me/certificates"), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<List<EventCertificateResponse>>(Ct) ?? [];
    }

    private Task<Guid> SeedPastConfirmedAsync(Guid userId, string activityTitle)
    {
        return SeedAssignmentAsync(
            userId,
            activityTitle,
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            PastStart,
            PastEnd
        );
    }

    [Fact]
    public async Task CertificatesAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/me/certificates"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CertificatesPastEventWithConfirmedAssignmentReturnsOwnCertificate()
    {
        var eventId = await SeedPastConfirmedAsync(
            TestSeedData.Users.MemberId,
            "Taller de robótica"
        );

        var certificates = await GetCertificatesAsMemberAsync();

        var certificate = certificates.Should().ContainSingle().Subject;
        certificate.EventId.Should().Be(eventId);
        certificate.UserId.Should().Be(TestSeedData.Users.MemberId);
        certificate.FirstName.Should().Be("Marta");
        certificate.LastName.Should().Be("Miembro");
        certificate.IsSelf.Should().BeTrue();
        certificate.EventTitle.Should().Be("Evento");
        certificate.EventStartsAt.Should().Be(PastStart);
        certificate.EventEndsAt.Should().Be(PastEnd);
        certificate.Code.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CertificatesPastEventWithUnconfirmedAssignmentReturnsNothing()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller pasado",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested,
            PastStart,
            PastEnd
        );

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().BeEmpty();
    }

    [Fact]
    public async Task CertificatesEventEndingTodayReturnsNothing()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller que acaba hoy",
            new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            TodayStart,
            TodayEnd
        );

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().BeEmpty();
    }

    [Fact]
    public async Task CertificatesUnfinishedEventWithConfirmedAssignmentReturnsNothing()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller futuro",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            FutureStart,
            FutureEnd
        );

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().BeEmpty();
    }

    [Fact]
    public async Task CertificatesSeveralConfirmedActivitiesInOneEventReturnsOneCertificate()
    {
        var eventId = await SeedPastConfirmedAsync(
            TestSeedData.Users.MemberId,
            "Primera actividad"
        );
        await SeedExtraAssignmentAsync(
            eventId,
            TestSeedData.Users.MemberId,
            "Segunda actividad",
            SeedIds.AssignmentStatusTypes.Confirmed
        );

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().ContainSingle().Which.EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task CertificatesParentAndChildConfirmedInOneEventReturnsOneCertificatePerMember()
    {
        var eventId = await SeedPastConfirmedAsync(
            TestSeedData.Users.MemberId,
            "Actividad de la madre"
        );
        await SeedExtraAssignmentAsync(
            eventId,
            TestSeedData.Users.MemberChildId,
            "Actividad del menor",
            SeedIds.AssignmentStatusTypes.Confirmed
        );

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().HaveCount(2);
        certificates.Should().AllSatisfy(certificate => certificate.EventId.Should().Be(eventId));
        certificates
            .Should()
            .Contain(certificate => certificate.UserId == TestSeedData.Users.MemberId && certificate.IsSelf);
        certificates
            .Should()
            .Contain(certificate =>
                certificate.UserId == TestSeedData.Users.MemberChildId && !certificate.IsSelf
            );
        certificates.Select(certificate => certificate.Code).Distinct(StringComparer.Ordinal).Should().HaveCount(2);
    }

    [Fact]
    public async Task CertificatesOtherHouseholdConfirmedAssignmentIsNotReturned()
    {
        await SeedPastConfirmedAsync(TestSeedData.Users.AdminId, "Taller ajeno");

        var certificates = await GetCertificatesAsMemberAsync();

        certificates.Should().BeEmpty();
    }

    [Fact]
    public async Task EventHistoryRatedPastEventEmbedsOwnRating()
    {
        var eventId = await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller pasado",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Confirmed,
            PastStart,
            PastEnd
        );
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.Add(
                new EventRating
                {
                    EventId = eventId,
                    UserId = TestSeedData.Users.MemberId,
                    Score = 4,
                    MostLiked = "El ambiente",
                    CreatedAt = SeededAt,
                }
            );
            return Task.CompletedTask;
        });

        var history = await GetHistoryAsMemberAsync();

        var entry = history.Should().ContainSingle().Subject;
        entry.MyRating.Should().NotBeNull();
        entry.MyRating.Score.Should().Be(4);
        entry.MyRating.MostLiked.Should().Be("El ambiente");
    }
}
