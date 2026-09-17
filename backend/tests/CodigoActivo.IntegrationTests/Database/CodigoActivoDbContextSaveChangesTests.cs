using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

/// <summary>
/// Exercises <c>CodigoActivoDbContext</c>'s translation of raw PostgreSQL unique-violation errors
/// (SQLSTATE 23505) into <see cref="UniqueConstraintViolationException"/> against a real database,
/// independently of the application handler that consumes it.
/// </summary>
public sealed class CodigoActivoDbContextSaveChangesTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid AuthorId = new("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid ThumbnailId = new("cccccccc-2222-2222-2222-222222222222");

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);

        db.Users.Add(NewUser(AuthorId, "author@test.local", "+34600000900"));
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

    private static Event NewEvent(Guid id)
    {
        return new Event
        {
            Id = id,
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
        };
    }

    [Fact]
    public async Task SaveChangesAsyncDuplicateEventRatingSubmissionThrowsUniqueConstraintViolationExceptionForItsPrimaryKey()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using (var db = postgres.CreateContext())
        {
            db.Users.Add(NewUser(userId, "submitter@test.local", "+34600000901"));
            db.Events.Add(NewEvent(eventId));
            db.EventRatingSubmissions.Add(
                new EventRatingSubmission { EventId = eventId, UserId = userId }
            );
            await ((IUnitOfWork)db).SaveChangesAsync(Ct);
        }

        // A second, independent context stands in for a concurrent request: it has no local
        // tracking entry for the row above, so the duplicate is only caught by the database's
        // real primary key constraint, exactly like two racing HTTP requests would.
        await using var second = postgres.CreateContext();
        second.EventRatingSubmissions.Add(
            new EventRatingSubmission { EventId = eventId, UserId = userId }
        );

        var act = () => ((IUnitOfWork)second).SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.ConstraintName.Should().Be(EventRatingSubmission.PrimaryKeyConstraintName);
    }

    [Fact]
    public async Task SaveChangesAsyncDuplicateUserEmailThrowsUniqueConstraintViolationExceptionWithADifferentConstraintName()
    {
        await using var db = postgres.CreateContext();
        const string DuplicateEmail = "duplicate@test.local";
        db.Users.Add(NewUser(Guid.NewGuid(), DuplicateEmail, "+34600000902"));
        await ((IUnitOfWork)db).SaveChangesAsync(Ct);

        db.Users.Add(NewUser(Guid.NewGuid(), DuplicateEmail, "+34600000903"));

        var act = () => ((IUnitOfWork)db).SaveChangesAsync(Ct);

        // A violation unrelated to event rating submissions still translates to the shared
        // exception type, but under its own constraint name: the handler's `when` filter, which
        // only matches EventRatingSubmission.PrimaryKeyConstraintName, would not catch this one and
        // would let it propagate instead of silently reporting "already submitted".
        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.ConstraintName.Should().NotBe(EventRatingSubmission.PrimaryKeyConstraintName);
        thrown.Which.ConstraintName.Should().Contain("email");
    }
}
