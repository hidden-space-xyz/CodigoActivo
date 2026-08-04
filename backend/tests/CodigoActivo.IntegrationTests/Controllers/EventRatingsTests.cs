using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventRatingsTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid EventId = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid ActivityId = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid EventThumbnailId = new("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid ActivityThumbnailId = new("dddddddd-0000-0000-0000-000000000004");

    private static readonly DateTimeOffset At = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly PastStart = new(2026, 6, 1);
    private static readonly DateOnly PastEnd = new(2026, 6, 2);
    private static readonly DateOnly FutureStart = new(2026, 8, 1);
    private static readonly DateOnly FutureEnd = new(2026, 8, 2);

    private static readonly SaveEventRatingRequest ValidRating = new(
        5,
        "La organización",
        "La cola de la comida",
        "Más talleres de robótica"
    );

    private Task SeedEventAsync(DateOnly startsAt, DateOnly endsAt, Guid? assignmentStatusId)
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.AddRange(Thumbnail(EventThumbnailId), Thumbnail(ActivityThumbnailId));
            db.Events.Add(
                new Event
                {
                    Id = EventId,
                    Title = "Jornada de puertas abiertas",
                    Subtitle = "Edición 2026",
                    Description = "{}",
                    EventStartsAt = startsAt,
                    EventEndsAt = endsAt,
                    SignupStartsAt = At,
                    SignupEndsAt = At,
                    ThumbnailId = EventThumbnailId,
                    CreatedAt = At,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = ActivityId,
                    Title = "Taller",
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = At,
                    ActivityEndsAt = At.AddHours(2),
                    EventId = EventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = ActivityThumbnailId,
                    CreatedAt = At,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            if (assignmentStatusId is { } statusId)
            {
                db.ActivityUserRoleAssignments.Add(
                    new ActivityUserRoleAssignment
                    {
                        UserId = TestSeedData.Users.MemberId,
                        ActivityId = ActivityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = statusId,
                    }
                );
            }

            return Task.CompletedTask;
        });
    }

    private static FileEntity Thumbnail(Guid id)
    {
        return new()
        {
            Id = id,
            Name = "thumb",
            Extension = "png",
            UploadedAt = At,
            UploadedBy = TestSeedData.Users.AdminId,
        };
    }

    [Fact]
    public async Task SaveRating_Anonymous_ReturnsUnauthorized()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = CreateClient();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveRating_UnknownEvent_ReturnsNotFound()
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{Guid.NewGuid()}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task SaveRating_EventNotFinished_ReturnsConflict()
    {
        await SeedEventAsync(FutureStart, FutureEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventRatingNotFinished);
    }

    [Fact]
    public async Task SaveRating_WithoutConfirmedAssignment_ReturnsConflict()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Requested);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventRatingAttendanceRequired);
    }

    [Fact]
    public async Task SaveRating_ScoreOutOfRange_ReturnsBadRequest()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            new SaveEventRatingRequest(6, null, null, null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveRating_PastEventWithConfirmedAssignment_PersistsAnswers()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rating = await response.ReadJsonAsync<EventRatingResponse>(Ct);
        rating.Should().NotBeNull();
        rating.Score.Should().Be(5);
        rating.MostLiked.Should().Be("La organización");
        rating.LeastLiked.Should().Be("La cola de la comida");
        rating.Suggestions.Should().Be("Más talleres de robótica");
        rating.UserId.Should().Be(TestSeedData.Users.MemberId);
        rating.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveRating_CalledTwice_UpdatesInsteadOfDuplicating()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var first = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            new SaveEventRatingRequest(2, "Otra cosa", null, null),
            Ct
        );

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var rating = await second.ReadJsonAsync<EventRatingResponse>(Ct);
        rating.Should().NotBeNull();
        rating.Score.Should().Be(2);
        rating.MostLiked.Should().Be("Otra cosa");
        rating.LeastLiked.Should().BeNull();
        rating.UpdatedAt.Should().NotBeNull();

        var adminClient = await LoginAsAdminAsync();
        using var listResponse = await adminClient.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);
        var page = await listResponse.ReadJsonAsync<PagedResult<EventRatingListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
    }

    [Fact]
    public async Task SaveRating_BlankAnswers_AreStoredAsNull()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            new SaveEventRatingRequest(0, "   ", "", null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rating = await response.ReadJsonAsync<EventRatingResponse>(Ct);
        rating!.Score.Should().Be(0);
        rating.MostLiked.Should().BeNull();
        rating.LeastLiked.Should().BeNull();
    }

    [Fact]
    public async Task Ratings_MemberUser_ReturnsForbidden()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ratings_UnknownEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{Guid.NewGuid()}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ratings_Admin_ReturnsAnonymousOpinions()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.Add(
                new EventRating
                {
                    EventId = EventId,
                    UserId = TestSeedData.Users.MemberId,
                    Score = 3,
                    MostLiked = "El taller",
                    CreatedAt = At,
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().NotContainAny("Marta", "Miembro", TestSeedData.Users.MemberId.ToString());

        var page = await response.ReadJsonAsync<PagedResult<EventRatingListItemResponse>>(Ct);
        var item = page!.Items.Should().ContainSingle().Subject;
        item.Score.Should().Be(3);
        item.MostLiked.Should().Be("El taller");
    }

    [Fact]
    public async Task EventSummary_WithRatings_ReturnsCountAndAverage()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.AddRange(
                new EventRating
                {
                    EventId = EventId,
                    UserId = TestSeedData.Users.MemberId,
                    Score = 5,
                    CreatedAt = At,
                },
                new EventRating
                {
                    EventId = EventId,
                    UserId = TestSeedData.Users.AdminId,
                    Score = 2,
                    CreatedAt = At,
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/reports/events/{EventId}/summary"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(Ct);
        summary!.RatingsCount.Should().Be(2);
        summary.RatingsAverage.Should().Be(3.5);
    }

    [Fact]
    public async Task EventSummary_WithoutRatings_ReturnsNullAverage()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/reports/events/{EventId}/summary"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(Ct);
        summary!.RatingsCount.Should().Be(0);
        summary.RatingsAverage.Should().BeNull();
    }
}
