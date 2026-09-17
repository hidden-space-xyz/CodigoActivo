// CA2100 flags every dynamic NpgsqlCommand.CommandText below. None of it carries user input: the
// database name is a test-generated GUID and every value column uses parameters, so the rule does
// not apply to this throwaway-database test fixture.
#pragma warning disable CA2100

using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

/// <summary>
/// Applies the <c>AnonymizeEventRatings</c> migration to a throwaway database seeded with the
/// pre-migration <c>event_ratings</c> shape (a <c>user_id</c> column and no
/// <c>event_rating_submissions</c> table), to prove the migration itself carries existing data
/// forward instead of only describing the target schema.
/// </summary>
public sealed class AnonymizeEventRatingsMigrationTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string PreviousMigrationId = "20260917090711_AddTwoFactorAuthentication";
    private const string TargetMigrationId = "20260917170401_AnonymizeEventRatings";

    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string databaseName = $"migration_test_{Guid.NewGuid():N}";
    private string temporaryConnectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(Ct);
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync(Ct);
        }

        temporaryConnectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(Ct);
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync(Ct);
    }

    private CodigoActivoDbContext CreateTemporaryContext()
    {
        return new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(
                    temporaryConnectionString,
                    npgsql =>
                        npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
                )
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }

    private static IMigrator GetMigrator(CodigoActivoDbContext db)
    {
        return db.GetInfrastructure().GetRequiredService<IMigrator>();
    }

    [Fact]
    public async Task MigrateAsyncAnonymizeEventRatingsPreservesRatingsAndPopulatesSubmissions()
    {
        var eventId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var raterOneId = Guid.NewGuid();
        var raterTwoId = Guid.NewGuid();
        var ratingOneId = Guid.NewGuid();
        var ratingTwoId = Guid.NewGuid();

        await using (var db = CreateTemporaryContext())
        {
            // Land on the schema exactly as it stood right before the migration under test: the
            // EF model in this assembly already reflects the anonymized (post-migration) shape, so
            // seeding through EF is only safe for the tables this migration does not touch.
            await GetMigrator(db).MigrateAsync(PreviousMigrationId, Ct);

            await new DatabaseSeeder(db).SeedAsync(Ct);

            db.Users.AddRange(
                NewUser(authorId, "author@migration-test.local", "+34600000910"),
                NewUser(raterOneId, "rater1@migration-test.local", "+34600000911"),
                NewUser(raterTwoId, "rater2@migration-test.local", "+34600000912")
            );
            db.Files.Add(
                new FileEntity
                {
                    Id = thumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = authorId,
                }
            );
            db.Events.Add(
                new Event
                {
                    Id = eventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 1, 1),
                    EventEndsAt = new DateOnly(2026, 1, 2),
                    SignupStartsAt = Fixed,
                    SignupEndsAt = Fixed.AddDays(1),
                    ThumbnailId = thumbnailId,
                    CreatedAt = Fixed,
                    CreatedBy = authorId,
                }
            );
            await db.SaveChangesAsync(Ct);
        }

        // Insert the legacy event_ratings rows directly through SQL: at this schema version the
        // table still carries user_id/created_at/updated_at, which the current EF model (used
        // above) no longer maps.
        await using (var connection = new NpgsqlConnection(temporaryConnectionString))
        {
            await connection.OpenAsync(Ct);
            await ExecuteAsync(
                connection,
                "INSERT INTO event_ratings (id, event_id, user_id, score, most_liked, "
                    + "least_liked, suggestions, created_at, updated_at) "
                    + "VALUES (@id, @eventId, @userId, 5, 'Genial', 'La cola', 'Más', "
                    + "@createdAt, null)",
                ("id", ratingOneId),
                ("eventId", eventId),
                ("userId", raterOneId),
                ("createdAt", Fixed)
            );
            await ExecuteAsync(
                connection,
                "INSERT INTO event_ratings (id, event_id, user_id, score, most_liked, "
                    + "least_liked, suggestions, created_at, updated_at) "
                    + "VALUES (@id, @eventId, @userId, 2, null, null, null, @createdAt, @updatedAt)",
                ("id", ratingTwoId),
                ("eventId", eventId),
                ("userId", raterTwoId),
                ("createdAt", Fixed),
                ("updatedAt", Fixed.AddDays(1))
            );
        }

        await using (var db = CreateTemporaryContext())
        {
            await GetMigrator(db).MigrateAsync(TargetMigrationId, Ct);
        }

        await using var verify = new NpgsqlConnection(temporaryConnectionString);
        await verify.OpenAsync(Ct);

        var submissionPairs = await QueryPairsAsync(
            verify,
            "SELECT event_id, user_id FROM event_rating_submissions"
        );
        submissionPairs.Should().BeEquivalentTo([(eventId, raterOneId), (eventId, raterTwoId)]);

        var ratingScores = await QueryScoresAsync(verify, ratingOneId, ratingTwoId);
        ratingScores[ratingOneId].Should().Be(5);
        ratingScores[ratingTwoId].Should().Be(2);

        var eventRatingColumns = await QueryColumnsAsync(verify, "event_ratings");
        eventRatingColumns.Should().NotContain("user_id");
        eventRatingColumns.Should().NotContain("created_at");
        eventRatingColumns.Should().NotContain("updated_at");

        // CLUSTER rewrites each table's heap but must preserve the indexes it clusters against
        // (and the other indexes created earlier in Up); a broken CLUSTER target or a dropped index
        // would otherwise pass unnoticed since the row-level assertions above do not exercise them.
        var submissionIndexes = await QueryIndexNamesAsync(verify, "event_rating_submissions");
        submissionIndexes.Should().Contain("pk_event_rating_submissions");
        var ratingIndexes = await QueryIndexNamesAsync(verify, "event_ratings");
        ratingIndexes.Should().Contain("ix_event_ratings_event_id");
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

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<(Guid EventId, Guid UserId)>> QueryPairsAsync(
        NpgsqlConnection connection,
        string sql
    )
    {
        var results = new List<(Guid, Guid)>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            results.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }

        return results;
    }

    private static async Task<Dictionary<Guid, int>> QueryScoresAsync(
        NpgsqlConnection connection,
        params Guid[] ids
    )
    {
        var results = new Dictionary<Guid, int>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, score FROM event_ratings WHERE id = ANY(@ids)";
        command.Parameters.AddWithValue("ids", ids);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            results[reader.GetGuid(0)] = reader.GetInt32(1);
        }

        return results;
    }

    private static async Task<HashSet<string>> QueryColumnsAsync(
        NpgsqlConnection connection,
        string table
    )
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static async Task<HashSet<string>> QueryIndexNamesAsync(
        NpgsqlConnection connection,
        string table
    )
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT indexname FROM pg_indexes WHERE tablename = @table";
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
