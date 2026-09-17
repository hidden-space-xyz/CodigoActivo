using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

/// <summary>
/// Exercises <see cref="EventRatingRepository.SubmitAsync"/> against a real database to prove that
/// the row lock actually serializes concurrent submissions for the same event, and that the
/// post-write reshuffle leaves every rating and submission for an event sharing a single
/// transaction identifier ("xmin") regardless of how many separate requests produced them.
/// </summary>
public sealed class EventRatingRepositorySubmitAsyncTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid AuthorId = new("eeeeeeee-1111-1111-1111-111111111111");
    private static readonly Guid ThumbnailId = new("eeeeeeee-2222-2222-2222-222222222222");
    private static readonly Guid EventId = new("eeeeeeee-3333-3333-3333-333333333333");

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);

        db.Users.Add(NewUser(AuthorId, "rating-author@test.local", "+34600000920"));
        db.Files.Add(
            new FileEntity
            {
                Id = ThumbnailId,
                Name = "thumb",
                Extension = "png",
                UploadedAt = Fixed,
                UploadedBy = AuthorId,
            }
        );
        db.Events.Add(
            new Event
            {
                Id = EventId,
                Title = "Evento",
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = new DateOnly(2026, 1, 1),
                EventEndsAt = new DateOnly(2026, 1, 2),
                SignupStartsAt = Fixed,
                SignupEndsAt = Fixed.AddDays(1),
                ThumbnailId = ThumbnailId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static User NewUser(Guid id, string email, string phone)
    {
        return new User
        {
            Id = id,
            FirstName = "Nombre",
            LastName = "Apellido",
            Email = email,
            Phone = phone,
            BirthDate = new DateOnly(1990, 1, 1),
            Gender = Gender.Other,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = Fixed,
        };
    }

    private static async Task<Guid> AddUserAsync(PostgresContainerFixture postgres, string suffix)
    {
        var userId = Guid.NewGuid();
        await using var db = postgres.CreateContext();
        db.Users.Add(NewUser(userId, $"rater-{suffix}@test.local", $"+3460000093{suffix}"));
        await db.SaveChangesAsync(Ct);
        return userId;
    }

    private static async Task<bool> SubmitAsync(
        PostgresContainerFixture postgres,
        Guid userId,
        int score
    )
    {
        await using var db = postgres.CreateContext();
        var repository = new EventRatingRepository(db);
        return await repository.SubmitAsync(new EventRating { EventId = EventId, Score = score }, userId, Ct);
    }

    private async Task<long> CountDistinctXminsAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(DISTINCT xmin::text) FROM ("
                + "SELECT xmin FROM event_ratings WHERE event_id = @eventId "
                + "UNION ALL "
                + "SELECT xmin FROM event_rating_submissions WHERE event_id = @eventId"
                + ") combined";
        command.Parameters.AddWithValue("eventId", EventId);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task SubmitAsyncSequentialSubmissionsShareASingleXminAcrossBothTables()
    {
        for (var i = 0; i < 3; i++)
        {
            var userId = await AddUserAsync(postgres, $"seq{i}");
            (await SubmitAsync(postgres, userId, i + 1)).Should().BeTrue();
        }

        (await CountDistinctXminsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitAsyncConcurrentDistinctUsersBothSucceedWithoutDuplicates()
    {
        var userOne = await AddUserAsync(postgres, "concurrentA");
        var userTwo = await AddUserAsync(postgres, "concurrentB");

        var results = await Task.WhenAll(
            SubmitAsync(postgres, userOne, 4),
            SubmitAsync(postgres, userTwo, 5)
        );

        results.Should().AllSatisfy(succeeded => succeeded.Should().BeTrue());

        await using var db = postgres.CreateContext();
        (await db.EventRatings.CountAsync(r => r.EventId == EventId, Ct)).Should().Be(2);
        (await db.EventRatingSubmissions.CountAsync(s => s.EventId == EventId, Ct)).Should().Be(2);
    }

    [Fact]
    public async Task SubmitAsyncConcurrentSameUserOnlyOneSucceeds()
    {
        var userId = await AddUserAsync(postgres, "duplicate");

        var results = await Task.WhenAll(
            SubmitAsync(postgres, userId, 1),
            SubmitAsync(postgres, userId, 2)
        );

        results.Should().ContainSingle(succeeded => succeeded);
        results.Should().ContainSingle(succeeded => !succeeded);

        await using var db = postgres.CreateContext();
        (await db.EventRatings.CountAsync(r => r.EventId == EventId, Ct)).Should().Be(1);
        (await db.EventRatingSubmissions.CountAsync(s => s.EventId == EventId, Ct)).Should().Be(1);
    }
}
