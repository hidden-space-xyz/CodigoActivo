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

    private static FileEntity Thumbnail(Guid id) =>
        new()
        {
            Id = id,
            Name = "thumb",
            Extension = "png",
            UploadedAt = SeededAt,
            UploadedBy = TestSeedData.Users.AdminId,
        };

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
        return await response.ReadJsonAsync<List<Application.DTOs.AssignedActivityResponse>>(
                TestContext.Current.CancellationToken
            ) ?? [];
    }

    [Fact]
    public async Task AssignedActivities_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/me/assigned-activities",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignedActivities_MemberWithAssignment_ReturnsProjectedAssignment()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller de robótica",
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            "/api/me/assigned-activities",
            TestContext.Current.CancellationToken
        );

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
    public async Task AssignedActivities_EventIdFilter_ExcludesOtherEventAssignments()
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
            $"/api/me/assigned-activities?eventId={targetEventId}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        var assigned = items.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Dentro del evento");
        assigned.EventId.Should().Be(targetEventId);
    }

    private static readonly DateOnly PastStart = new(2026, 6, 1);
    private static readonly DateOnly PastEnd = new(2026, 6, 2);
    private static readonly DateOnly FutureStart = new(2026, 8, 1);
    private static readonly DateOnly FutureEnd = new(2026, 8, 2);

    private static async Task<List<EventHistoryResponse>> ReadHistoryAsync(
        HttpResponseMessage response
    )
    {
        return await response.ReadJsonAsync<List<EventHistoryResponse>>(
                TestContext.Current.CancellationToken
            ) ?? [];
    }

    private async Task<List<EventHistoryResponse>> GetHistoryAsMemberAsync()
    {
        var client = await LoginAsMemberAsync();
        var response = await client.GetAsync(
            "/api/me/event-history",
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadHistoryAsync(response);
    }

    [Fact]
    public async Task EventHistory_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/me/event-history",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EventHistory_PastEventWithoutConfirmedAssignment_OmitsEvent()
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
    public async Task EventHistory_PastEventWithConfirmedAssignment_IncludesEventMarkedAsPast()
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
    public async Task EventHistory_FutureEventWithRequestedAssignment_KeepsAssignmentAndStatus()
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
    public async Task EventHistory_ChildAssignment_IncludesHouseholdMemberAsNotSelf()
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
    public async Task EventHistory_UpcomingAndPastEvents_OrdersUpcomingFirst()
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
    public async Task EventHistory_OtherUsersAssignment_IsNotReturned()
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

    [Fact]
    public async Task EventHistory_RatedPastEvent_EmbedsOwnRating()
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
        entry.MyRating!.Score.Should().Be(4);
        entry.MyRating.MostLiked.Should().Be("El ambiente");
    }
}
