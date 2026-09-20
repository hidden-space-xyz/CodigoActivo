using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Seeds a past or future event with a single activity, optionally granting the member an
    /// assignment with the requested status. When <paramref name="childAssignmentStatusId"/> is
    /// supplied, the member's seeded minor gets the assignment instead, so tests can exercise
    /// rating on behalf of a dependent.
    /// </summary>
    private Task SeedEventAsync(
        DateOnly startsAt,
        DateOnly endsAt,
        Guid? assignmentStatusId,
        Guid? childAssignmentStatusId = null
    )
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

            if (childAssignmentStatusId is { } childStatusId)
            {
                db.ActivityUserRoleAssignments.Add(
                    new ActivityUserRoleAssignment
                    {
                        UserId = TestSeedData.Users.MemberChildId,
                        ActivityId = ActivityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = childStatusId,
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
    public async Task SaveRatingAnonymousReturnsUnauthorized()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveRatingUnknownEventReturnsNotFound()
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{Guid.NewGuid()}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task SaveRatingEventNotFinishedReturnsConflict()
    {
        await SeedEventAsync(FutureStart, FutureEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventRatingNotFinished);
    }

    [Fact]
    public async Task SaveRatingWithoutConfirmedAssignmentReturnsConflict()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Requested);
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventRatingAttendanceRequired);
    }

    [Fact]
    public async Task SaveRatingScoreOutOfRangeReturnsBadRequest()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            new SaveEventRatingRequest(6, null, null, null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveRatingPastEventWithConfirmedAssignmentReturnsNoContentAndPersistsAnonymousRating()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Factory.QueryAsync(async db =>
        {
            var ratings = await db.EventRatings.Where(r => r.EventId == EventId).ToListAsync(Ct);
            var rating = ratings.Should().ContainSingle().Subject;
            rating.Score.Should().Be(5);
            rating.MostLiked.Should().Be("La organización");
            rating.LeastLiked.Should().Be("La cola de la comida");
            rating.Suggestions.Should().Be("Más talleres de robótica");
            return true;
        });
    }

    [Fact]
    public async Task SaveRatingViaConfirmedChildAttendanceSucceeds()
    {
        await SeedEventAsync(
            PastStart,
            PastEnd,
            assignmentStatusId: null,
            childAssignmentStatusId: SeedIds.AssignmentStatusTypes.Confirmed
        );
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Factory.QueryAsync(async db =>
        {
            (await db.EventRatings.CountAsync(r => r.EventId == EventId, Ct)).Should().Be(1);
            return true;
        });
    }

    [Fact]
    public async Task SaveRatingWithoutOwnOrChildAttendanceReturnsConflict()
    {
        await SeedEventAsync(
            PastStart,
            PastEnd,
            assignmentStatusId: null,
            childAssignmentStatusId: SeedIds.AssignmentStatusTypes.Requested
        );
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventRatingAttendanceRequired);
    }

    [Fact]
    public async Task SaveRatingCalledTwiceBySameUserStoresBothAnonymousRatings()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var first = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );
        using var second = await client.PostJsonAsync(
            $"/api/events/{EventId}/rating",
            new SaveEventRatingRequest(1, "Otra cosa", "Otra más", "Otra sugerencia"),
            Ct
        );

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Factory.QueryAsync(async db =>
        {
            var ratings = await db.EventRatings.Where(r => r.EventId == EventId).ToListAsync(Ct);
            ratings
                .Should()
                .HaveCount(2, "a second opinion is stored next to the first, not over it");
            ratings.Select(r => r.Score).Should().BeEquivalentTo([5, 1]);
            ratings.Select(r => r.Id).Should().OnlyHaveUniqueItems();
            return true;
        });
    }

    [Fact]
    public async Task SaveRatingObsoletePutMethodIsNotAvailable()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        using var response = await client.PutJsonAsync(
            $"/api/events/{EventId}/rating",
            ValidRating,
            Ct
        );

        response
            .StatusCode.Should()
            .BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RatingsMemberUserReturnsForbidden()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RatingsUnknownEventReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/events/{Guid.NewGuid()}/ratings"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RatingsAdminReturnsAnonymousOpinionsWithoutDateFields()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.Add(
                new EventRating
                {
                    EventId = EventId,
                    Score = 3,
                    MostLiked = "El taller",
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.Should()
            .NotContainAny(
                "Marta",
                "Miembro",
                TestSeedData.Users.MemberId.ToString(),
                "createdAt",
                "updatedAt"
            );

        var page = await response.ReadJsonAsync<PagedResult<EventRatingListItemResponse>>(Ct);
        var item = page!.Items.Should().ContainSingle().Subject;
        item.Score.Should().Be(3);
        item.MostLiked.Should().Be("El taller");
    }

    [Fact]
    public async Task RatingsDefaultSortOrdersByDescendingScore()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.AddRange(
                new EventRating { EventId = EventId, Score = 1 },
                new EventRating { EventId = EventId, Score = 5 },
                new EventRating { EventId = EventId, Score = 3 }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{EventId}/ratings"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventRatingListItemResponse>>(Ct);
        page!.Items.Select(i => i.Score).Should().Equal(5, 3, 1);
    }

    [Fact]
    public async Task RatingsAscendingSortOrdersByScore()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.AddRange(
                new EventRating { EventId = EventId, Score = 1 },
                new EventRating { EventId = EventId, Score = 5 },
                new EventRating { EventId = EventId, Score = 3 }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/events/{EventId}/ratings?sort=score"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventRatingListItemResponse>>(Ct);
        page!.Items.Select(i => i.Score).Should().Equal(1, 3, 5);
    }

    [Fact]
    public async Task EventRatingsTableHasNoUserOrDateColumns()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);

        var columns = await Factory.QueryAsync(async db =>
        {
            var connection = db.Database.GetDbConnection();
            var wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed)
            {
                await connection.OpenAsync(Ct);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT column_name FROM information_schema.columns "
                    + "WHERE table_name = 'event_ratings'";
                var names = new List<string>();
                await using var reader = await command.ExecuteReaderAsync(Ct);
                while (await reader.ReadAsync(Ct))
                {
                    names.Add(reader.GetString(0));
                }

                return names;
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync();
                }
            }
        });

        columns
            .Should()
            .NotBeEmpty()
            .And.NotContain("user_id")
            .And.NotContain("created_at")
            .And.NotContain("updated_at");
        columns
            .Should()
            .BeEquivalentTo([
                "id",
                "event_id",
                "score",
                "most_liked",
                "least_liked",
                "suggestions",
            ]);
    }

    [Fact]
    public async Task EventSummaryWithRatingsReturnsCountAndAverage()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        await Factory.SeedAsync(db =>
        {
            db.EventRatings.AddRange(
                new EventRating { EventId = EventId, Score = 5 },
                new EventRating { EventId = EventId, Score = 2 }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/reports/events/{EventId}/summary"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(Ct);
        summary!.RatingsCount.Should().Be(2);
        summary.RatingsAverage.Should().Be(3.5);
    }

    [Fact]
    public async Task EventSummaryWithoutRatingsReturnsNullAverage()
    {
        await SeedEventAsync(PastStart, PastEnd, SeedIds.AssignmentStatusTypes.Confirmed);
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/reports/events/{EventId}/summary"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(Ct);
        summary!.RatingsCount.Should().Be(0);
        summary.RatingsAverage.Should().BeNull();
    }
}
