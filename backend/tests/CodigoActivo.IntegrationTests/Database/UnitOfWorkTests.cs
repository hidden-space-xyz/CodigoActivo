using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class UnitOfWorkTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid FileId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid EventId = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid ActivityId = new("cccccccc-0000-0000-0000-000000000004");
    private const string UserEmail = "unit-of-work@codigoactivo.test";

    private static readonly global::CodigoActivo.Domain.Events.EventId SeededEventId =
        global::CodigoActivo.Domain.Events.EventId.From(EventId);

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);

        db.Users.Add(NewUser(UserId, UserEmail));
        db.Files.Add(
            Persisted.As<StoredFile>(
                new
                {
                    Id = FileId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = UserId,
                }
            )
        );
        db.Events.Add(
            Persisted.As<Event>(
                new
                {
                    Id = EventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    EventStartsAt = new DateOnly(2026, 7, 1),
                    EventEndsAt = new DateOnly(2026, 7, 2),
                    SignupStartsAt = Fixed,
                    SignupEndsAt = Fixed.AddDays(30),
                    ThumbnailId = FileId,
                    CreatedAt = Fixed,
                    CreatedBy = UserId,
                }
            )
        );
        db.Activities.Add(
            Persisted.As<Activity>(
                new
                {
                    Id = ActivityId,
                    Title = "Actividad",
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = Fixed.AddDays(40),
                    ActivityEndsAt = Fixed.AddDays(40).AddHours(2),
                    EventId = EventId,
                    Modality = ActivityModality.Presencial,
                    ThumbnailId = FileId,
                    CreatedAt = Fixed,
                    CreatedBy = UserId,
                }
            )
        );
        db.Assignments.Add(NewAssignment(ActivityId, KnownIds.ActivityRoleTypes.Participant));
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static User NewUser(Guid id, string email)
    {
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = "Unidad",
                LastName = "De Trabajo",
                Email = email,
                Gender = Gender.Other,
                Status = UserStatus.Active,
                UserType = UserType.Member,
                CreatedAt = Fixed,
            }
        );
    }

    private static Event FeaturedEvent()
    {
        return Persisted.As<Event>(
            new
            {
                Id = Guid.NewGuid(),
                Title = "Destacado",
                Subtitle = "Sub",
                EventStartsAt = new DateOnly(2026, 7, 1),
                EventEndsAt = new DateOnly(2026, 7, 2),
                SignupStartsAt = Fixed,
                SignupEndsAt = Fixed.AddDays(30),
                Featured = true,
                ThumbnailId = FileId,
                CreatedAt = Fixed,
                CreatedBy = UserId,
            }
        );
    }

    private static NewsItem FeaturedNewsItem()
    {
        return Persisted.As<NewsItem>(
            new
            {
                Id = Guid.NewGuid(),
                Title = "Destacada",
                Subtitle = "Sub",
                Featured = true,
                ThumbnailId = FileId,
                CreatedAt = Fixed,
                CreatedBy = UserId,
            }
        );
    }

    private static Assignment NewAssignment(Guid activityId, Guid roleTypeId)
    {
        return Persisted.As<Assignment>(
            new
            {
                UserId = UserId,
                ActivityId = activityId,
                Role = CatalogIds.ActivityRoles.ValueOf(roleTypeId),
                Status = AssignmentStatus.Requested,
                CreatedAt = Fixed,
            }
        );
    }

    [Fact]
    public async Task SaveChangesAsyncSecondAssignmentOfAUserToAnActivityThrowsForAssignments()
    {
        await using var db = postgres.CreateContext();
        db.Assignments.Add(NewAssignment(ActivityId, KnownIds.ActivityRoleTypes.Leader));
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<Assignment>();
        thrown.Which.InnerException.Should().BeOfType<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsyncDuplicateEmailNamesTheUserEntity()
    {
        await using var db = postgres.CreateContext();
        db.Users.Add(NewUser(Guid.NewGuid(), UserEmail));
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<User>();
    }

    [Fact]
    public async Task SaveChangesAsyncSecondFeaturedEventNamesTheEventEntity()
    {
        await using var db = postgres.CreateContext();
        var seeded = await db.Events.SingleAsync(
            e => e.Id == global::CodigoActivo.Domain.Events.EventId.From(EventId),
            Ct
        );
        seeded.Feature();
        await db.SaveChangesAsync(Ct);
        db.Events.Add(FeaturedEvent());
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<Event>();
    }

    [Fact]
    public async Task SaveChangesAsyncSecondFeaturedNewsItemNamesTheNewsItemEntity()
    {
        await using var db = postgres.CreateContext();
        db.News.AddRange(FeaturedNewsItem(), FeaturedNewsItem());
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<NewsItem>();
    }

    [Fact]
    public async Task SaveChangesAsyncOtherDatabaseErrorsAreNotTranslated()
    {
        await using var db = postgres.CreateContext();
        db.Assignments.Add(NewAssignment(Guid.NewGuid(), KnownIds.ActivityRoleTypes.Participant));
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        await act.Should().ThrowExactlyAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsyncReplacingTheRoleOfAnAssignmentKeepsOneRow()
    {
        await using (var db = postgres.CreateContext())
        {
            var current = await db.Assignments.SingleAsync(
                a =>
                    a.UserId == global::CodigoActivo.Domain.Users.UserId.From(UserId)
                    && a.ActivityId
                        == global::CodigoActivo.Domain.Activities.ActivityId.From(ActivityId),
                Ct
            );
            db.Assignments.Remove(current);
            db.Assignments.Add(NewAssignment(ActivityId, KnownIds.ActivityRoleTypes.Volunteer));
            var uow = TestUnitOfWork.For(db);

            await uow.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify
            .Assignments.Where(a =>
                a.UserId == global::CodigoActivo.Domain.Users.UserId.From(UserId)
                && a.ActivityId
                    == global::CodigoActivo.Domain.Activities.ActivityId.From(ActivityId)
            )
            .ToListAsync(Ct);
        stored.Should().ContainSingle().Which.Role.Should().Be(ActivityRole.Volunteer);
    }

    [Fact]
    public async Task SaveChangesAsyncRefusedCommitKeepsItsEventsForTheNextCommit()
    {
        await using var db = postgres.CreateContext();
        var publisher = new RecordingPublisher();
        var uow = new UnitOfWork(db, publisher, NullLogger<UnitOfWork>.Instance);
        (await db.Events.SingleAsync(e => e.Id == SeededEventId, Ct)).Feature();
        var duplicate = NewUser(Guid.NewGuid(), UserEmail);
        db.Users.Add(duplicate);

        var refused = () => uow.SaveChangesAsync(Ct);
        await refused.Should().ThrowAsync<UniqueConstraintViolationException>();
        db.Entry(duplicate).State = EntityState.Detached;
        await uow.SaveChangesAsync(Ct);

        publisher.Published.Should().Equal(new EventFeaturedChanged(SeededEventId, true));
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncDeadlockedAttemptKeepsTheChangesStagedBeforeIt()
    {
        await using (var db = postgres.CreateContext())
        {
            var publisher = new RecordingPublisher();
            var uow = new UnitOfWork(db, publisher, NullLogger<UnitOfWork>.Instance);
            (await db.Events.SingleAsync(e => e.Id == SeededEventId, Ct)).Feature();
            var attempts = 0;

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    attempts++;
                    await uow.SaveChangesAsync(attempt);
                    return attempts > 1 ? true : throw Deadlock();
                },
                Ct
            );

            attempts.Should().Be(2);
            publisher.Published.Should().Equal(new EventFeaturedChanged(SeededEventId, true));
        }

        await using var verify = postgres.CreateContext();
        (await verify.Events.SingleAsync(e => e.Id == SeededEventId, Ct))
            .Featured.Should()
            .BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncDeadlockedAttemptRunsAgainFromTheStateItStarted()
    {
        var previous = FeaturedEvent();
        await using (var seed = postgres.CreateContext())
        {
            seed.Events.Add(previous);
            await seed.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext())
        {
            var publisher = new RecordingPublisher();
            var uow = new UnitOfWork(db, publisher, NullLogger<UnitOfWork>.Instance);
            var chosen = await db.Events.SingleAsync(e => e.Id == SeededEventId, Ct);
            var attempts = 0;

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    attempts++;
                    foreach (
                        var featured in await db.Events.Where(e => e.Featured).ToListAsync(attempt)
                    )
                    {
                        featured.Unfeature();
                    }

                    await uow.SaveChangesAsync(attempt);
                    chosen.Feature();
                    if (attempts is 1)
                    {
                        throw Deadlock();
                    }

                    await uow.SaveChangesAsync(attempt);
                    return true;
                },
                Ct
            );

            attempts.Should().Be(2);
            publisher
                .Published.Should()
                .Equal(
                    new EventFeaturedChanged(previous.Id, false),
                    new EventFeaturedChanged(SeededEventId, true)
                );
        }

        await using var verify = postgres.CreateContext();
        (await verify.Events.Where(e => e.Featured).Select(e => e.Id).ToListAsync(Ct))
            .Should()
            .Equal(SeededEventId);
    }

    private static PostgresException Deadlock()
    {
        return new PostgresException(
            "deadlock detected",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.DeadlockDetected
        );
    }

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<IDomainEvent> Published { get; } = [];

        public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
        {
            Published.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }
}
