using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = FileId,
                    CreatedAt = Fixed,
                    CreatedBy = UserId,
                }
            )
        );
        db.Assignments.Add(NewAssignment(ActivityId, SeedIds.ActivityRoleTypes.Participant));
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
                UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                UserTypeId = SeedIds.UserTypes.Member,
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
                ActivityRoleTypeId = roleTypeId,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
                CreatedAt = Fixed,
            }
        );
    }

    [Fact]
    public async Task SaveChangesAsyncSecondAssignmentOfAUserToAnActivityThrowsForAssignments()
    {
        await using var db = postgres.CreateContext();
        db.Assignments.Add(NewAssignment(ActivityId, SeedIds.ActivityRoleTypes.Leader));
        IUnitOfWork uow = db;

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
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<User>();
    }

    [Fact]
    public async Task SaveChangesAsyncSecondFeaturedEventNamesTheEventEntity()
    {
        await using var db = postgres.CreateContext();
        var seeded = await db.Events.SingleAsync(e => e.Id == EventId, Ct);
        seeded.Feature();
        await db.SaveChangesAsync(Ct);
        db.Events.Add(FeaturedEvent());
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<Event>();
    }

    [Fact]
    public async Task SaveChangesAsyncSecondFeaturedNewsItemNamesTheNewsItemEntity()
    {
        await using var db = postgres.CreateContext();
        db.News.AddRange(FeaturedNewsItem(), FeaturedNewsItem());
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<NewsItem>();
    }

    [Fact]
    public async Task SaveChangesAsyncOtherDatabaseErrorsAreNotTranslated()
    {
        await using var db = postgres.CreateContext();
        db.Assignments.Add(NewAssignment(Guid.NewGuid(), SeedIds.ActivityRoleTypes.Participant));
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        await act.Should().ThrowExactlyAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsyncReplacingTheRoleOfAnAssignmentKeepsOneRow()
    {
        await using (var db = postgres.CreateContext())
        {
            var current = await db.Assignments.SingleAsync(
                a => a.UserId == UserId && a.ActivityId == ActivityId,
                Ct
            );
            db.Assignments.Remove(current);
            db.Assignments.Add(NewAssignment(ActivityId, SeedIds.ActivityRoleTypes.Volunteer));
            IUnitOfWork uow = db;

            await uow.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify
            .Assignments.Where(a => a.UserId == UserId && a.ActivityId == ActivityId)
            .ToListAsync(Ct);
        stored
            .Should()
            .ContainSingle()
            .Which.ActivityRoleTypeId.Should()
            .Be(SeedIds.ActivityRoleTypes.Volunteer);
    }
}
