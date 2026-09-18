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
/// Applies the <c>AddMultipleEventTermsDocuments</c> migration to throwaway databases, to prove the
/// migration itself carries existing data forward (and the <c>Down</c> migration collapses it back)
/// instead of only describing the target schema. Two independent databases are used, one per
/// direction, because <c>Up</c> must start from the single-document pre-migration shape (seeded via
/// raw SQL, since the current EF model no longer maps it) while <c>Down</c> must start from the
/// multi-document post-migration shape the current EF model does map.
/// </summary>
public sealed class MultipleEventTermsDocumentsMigrationTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string PreviousMigrationId = "20260917170401_AnonymizeEventRatings";
    private const string TargetMigrationId = "20260917212636_AddMultipleEventTermsDocuments";

    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string upDatabaseName = $"migration_test_up_{Guid.NewGuid():N}";
    private readonly string downDatabaseName = $"migration_test_down_{Guid.NewGuid():N}";
    private string upConnectionString = string.Empty;
    private string downConnectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        upConnectionString = await CreateDatabaseAsync(upDatabaseName);
        downConnectionString = await CreateDatabaseAsync(downDatabaseName);
    }

    public async ValueTask DisposeAsync()
    {
        await DropDatabaseAsync(upDatabaseName);
        await DropDatabaseAsync(downDatabaseName);
    }

    private async Task<string> CreateDatabaseAsync(string databaseName)
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(Ct);
        await using var create = admin.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await create.ExecuteNonQueryAsync(Ct);

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(Ct);
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync(Ct);
    }

    private static CodigoActivoDbContext CreateContext(string connectionString)
    {
        return new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(
                    connectionString,
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

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @table)";
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task MigrateAsyncAddMultipleEventTermsDocumentsBackfillsRequiredDocumentAndRenamesDecidedAt()
    {
        var linkedEventId = Guid.NewGuid();
        var unlinkedEventId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var raterId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var otherThumbnailId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        var acceptedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var db = CreateContext(upConnectionString))
        {
            await GetMigrator(db).MigrateAsync(PreviousMigrationId, Ct);
            await new DatabaseSeeder(db).SeedAsync(Ct);

            db.Users.AddRange(
                NewUser(authorId, "author@terms-migration.local", "+34600000920"),
                NewUser(raterId, "rater@terms-migration.local", "+34600000921")
            );
            db.Files.AddRange(
                new FileEntity
                {
                    Id = thumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = authorId,
                },
                new FileEntity
                {
                    Id = otherThumbnailId,
                    Name = "thumb2",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = authorId,
                }
            );
            db.TermsDocuments.Add(
                new TermsDocument
                {
                    Id = termsDocumentId,
                    Name = "Reglamento",
                    Description = "{}",
                }
            );
            db.Events.Add(
                new Event
                {
                    Id = linkedEventId,
                    Title = "Evento con términos",
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
            db.Events.Add(
                new Event
                {
                    Id = unlinkedEventId,
                    Title = "Evento sin términos",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 1, 1),
                    EventEndsAt = new DateOnly(2026, 1, 2),
                    SignupStartsAt = Fixed,
                    SignupEndsAt = Fixed.AddDays(1),
                    ThumbnailId = otherThumbnailId,
                    CreatedAt = Fixed,
                    CreatedBy = authorId,
                }
            );
            await db.SaveChangesAsync(Ct);
        }

        await using (var connection = new NpgsqlConnection(upConnectionString))
        {
            await connection.OpenAsync(Ct);
            await ExecuteAsync(
                connection,
                "UPDATE events SET terms_document_id = @termsDocumentId WHERE id = @eventId",
                ("termsDocumentId", termsDocumentId),
                ("eventId", linkedEventId)
            );
            await ExecuteAsync(
                connection,
                "INSERT INTO event_terms_acceptances (event_id, user_id, terms_document_id, accepted_at) "
                    + "VALUES (@eventId, @userId, @termsDocumentId, @acceptedAt)",
                ("eventId", linkedEventId),
                ("userId", raterId),
                ("termsDocumentId", termsDocumentId),
                ("acceptedAt", acceptedAt)
            );
        }

        await using (var db = CreateContext(upConnectionString))
        {
            await GetMigrator(db).MigrateAsync(TargetMigrationId, Ct);
        }

        await using var verify = new NpgsqlConnection(upConnectionString);
        await verify.OpenAsync(Ct);

        var eventColumns = await QueryColumnsAsync(verify, "events");
        eventColumns.Should().NotContain(
            "terms_document_id",
            "the single-document column is replaced by the bridge table"
        );

        await using (var bridgeCommand = verify.CreateCommand())
        {
            bridgeCommand.CommandText =
                "SELECT event_id, terms_document_id, is_required, display_order "
                + "FROM event_terms_documents";
            await using var reader = await bridgeCommand.ExecuteReaderAsync(Ct);
            var rows = new List<(Guid EventId, Guid TermsDocumentId, bool IsRequired, int DisplayOrder)>();
            while (await reader.ReadAsync(Ct))
            {
                rows.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetBoolean(2), reader.GetInt32(3)));
            }

            rows.Should().Equal([(linkedEventId, termsDocumentId, true, 0)]);
        }

        var acceptanceColumns = await QueryColumnsAsync(verify, "event_terms_acceptances");
        acceptanceColumns.Should().Contain("accepted");
        acceptanceColumns.Should().Contain("decided_at");
        acceptanceColumns.Should().NotContain(
            "accepted_at",
            "accepted_at is renamed to decided_at, not duplicated"
        );

        await using (var acceptanceCommand = verify.CreateCommand())
        {
            acceptanceCommand.CommandText =
                "SELECT accepted, decided_at FROM event_terms_acceptances "
                + "WHERE event_id = @eventId AND user_id = @userId AND terms_document_id = @termsDocumentId";
            acceptanceCommand.Parameters.AddWithValue("eventId", linkedEventId);
            acceptanceCommand.Parameters.AddWithValue("userId", raterId);
            acceptanceCommand.Parameters.AddWithValue("termsDocumentId", termsDocumentId);
            await using var reader = await acceptanceCommand.ExecuteReaderAsync(Ct);
            (await reader.ReadAsync(Ct)).Should().BeTrue();
            reader.GetBoolean(0).Should().BeTrue(
                "every pre-migration row only ever recorded an acceptance"
            );
            reader.GetFieldValue<DateTimeOffset>(1).Should().Be(acceptedAt);
        }
    }

    [Fact]
    public async Task MigrateAsyncAddMultipleEventTermsDocumentsDownCollapsesAcceptancesAndRepopulatesEventColumn()
    {
        var eventId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var requiredDocumentId = Guid.NewGuid();
        var optionalDocumentId = Guid.NewGuid();
        var extraOptionalDocumentId = Guid.NewGuid();
        var earliestAcceptedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var laterAcceptedAt = earliestAcceptedAt.AddDays(1);
        var rejectedAt = earliestAcceptedAt.AddDays(-1);

        await using (var db = CreateContext(downConnectionString))
        {
            await db.Database.MigrateAsync(Ct);
            await new DatabaseSeeder(db).SeedAsync(Ct);

            db.Users.Add(NewUser(authorId, "author@terms-down.local", "+34600000930"));
            db.Users.Add(NewUser(userId, "user@terms-down.local", "+34600000931"));
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
            db.TermsDocuments.AddRange(
                new TermsDocument
                {
                    Id = requiredDocumentId,
                    Name = "Obligatorio",
                    Description = "{}",
                },
                new TermsDocument
                {
                    Id = optionalDocumentId,
                    Name = "Opcional",
                    Description = "{}",
                },
                new TermsDocument
                {
                    Id = extraOptionalDocumentId,
                    Name = "Opcional adicional",
                    Description = "{}",
                }
            );
            var ev = new Event
            {
                Id = eventId,
                Title = "Evento con varios documentos",
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = new DateOnly(2026, 3, 1),
                EventEndsAt = new DateOnly(2026, 3, 2),
                SignupStartsAt = Fixed,
                SignupEndsAt = Fixed.AddDays(1),
                ThumbnailId = thumbnailId,
                CreatedAt = Fixed,
                CreatedBy = authorId,
            };
            ev.TermsDocuments.Add(
                new EventTermsDocument
                {
                    TermsDocumentId = optionalDocumentId,
                    IsRequired = false,
                    DisplayOrder = 0,
                }
            );
            ev.TermsDocuments.Add(
                new EventTermsDocument
                {
                    TermsDocumentId = requiredDocumentId,
                    IsRequired = true,
                    DisplayOrder = 1,
                }
            );
            db.Events.Add(ev);
            await db.SaveChangesAsync(Ct);

            db.EventTermsAcceptances.AddRange(
                new EventTermsAcceptance
                {
                    EventId = eventId,
                    UserId = userId,
                    TermsDocumentId = optionalDocumentId,
                    Accepted = false,
                    DecidedAt = rejectedAt,
                },
                new EventTermsAcceptance
                {
                    EventId = eventId,
                    UserId = userId,
                    TermsDocumentId = requiredDocumentId,
                    Accepted = true,
                    DecidedAt = earliestAcceptedAt,
                },
                new EventTermsAcceptance
                {
                    EventId = eventId,
                    UserId = userId,
                    TermsDocumentId = extraOptionalDocumentId,
                    Accepted = true,
                    DecidedAt = laterAcceptedAt,
                }
            );
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = CreateContext(downConnectionString))
        {
            await GetMigrator(db).MigrateAsync(PreviousMigrationId, Ct);
        }

        await using var verify = new NpgsqlConnection(downConnectionString);
        await verify.OpenAsync(Ct);

        (await TableExistsAsync(verify, "event_terms_documents")).Should().BeFalse(
            "the bridge table only exists after this migration"
        );

        var acceptanceColumns = await QueryColumnsAsync(verify, "event_terms_acceptances");
        acceptanceColumns.Should().Contain("accepted_at");
        acceptanceColumns.Should().NotContain(
            "accepted",
            "the per-document accepted flag has no meaning once collapsed to one row per user"
        );
        acceptanceColumns.Should().NotContain("decided_at");

        await using (var acceptanceCommand = verify.CreateCommand())
        {
            acceptanceCommand.CommandText =
                "SELECT terms_document_id, accepted_at FROM event_terms_acceptances "
                + "WHERE event_id = @eventId AND user_id = @userId";
            acceptanceCommand.Parameters.AddWithValue("eventId", eventId);
            acceptanceCommand.Parameters.AddWithValue("userId", userId);
            await using var reader = await acceptanceCommand.ExecuteReaderAsync(Ct);
            (await reader.ReadAsync(Ct)).Should().BeTrue();
            reader.GetGuid(0).Should().Be(
                requiredDocumentId,
                "the rejection is discarded first, leaving the earliest surviving acceptance"
            );
            reader.GetFieldValue<DateTimeOffset>(1).Should().Be(earliestAcceptedAt);
            (await reader.ReadAsync(Ct)).Should().BeFalse(
                "only one row per (event, user) must survive the collapse"
            );
        }

        await using (var eventCommand = verify.CreateCommand())
        {
            eventCommand.CommandText = "SELECT terms_document_id FROM events WHERE id = @eventId";
            eventCommand.Parameters.AddWithValue("eventId", eventId);
            var repopulated = await eventCommand.ExecuteScalarAsync(Ct);
            repopulated.Should().Be(
                requiredDocumentId,
                "required documents are preferred over display order when repopulating the single column"
            );
        }
    }
}
