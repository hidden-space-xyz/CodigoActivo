using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
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
            new FileEntity
            {
                Id = FileId,
                Name = "thumb",
                Extension = "png",
                UploadedAt = Fixed,
                UploadedBy = UserId,
            }
        );
        db.Events.Add(
            new Event
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
        );
        db.Activities.Add(
            new Activity
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
        );
        db.ActivityUserRoleAssignments.Add(
            Assignment(ActivityId, SeedIds.ActivityRoleTypes.Participant)
        );
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static User NewUser(Guid id, string email)
    {
        return new User
        {
            Id = id,
            FirstName = "Unidad",
            LastName = "De Trabajo",
            Email = email,
            Gender = Gender.Other,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = Fixed,
        };
    }

    private static ActivityUserRoleAssignment Assignment(Guid activityId, Guid roleTypeId)
    {
        return new ActivityUserRoleAssignment
        {
            UserId = UserId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
            CreatedAt = Fixed,
        };
    }

    [Fact]
    public async Task SaveChangesAsyncSecondAssignmentOfAUserToAnActivityThrowsForAssignments()
    {
        await using var db = postgres.CreateContext();
        db.ActivityUserRoleAssignments.Add(
            Assignment(ActivityId, SeedIds.ActivityRoleTypes.Leader)
        );
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<ActivityUserRoleAssignment>();
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
    public async Task SaveChangesAsyncOtherDatabaseErrorsAreNotTranslated()
    {
        await using var db = postgres.CreateContext();
        db.ActivityUserRoleAssignments.Add(
            Assignment(Guid.NewGuid(), SeedIds.ActivityRoleTypes.Participant)
        );
        IUnitOfWork uow = db;

        var act = () => uow.SaveChangesAsync(Ct);

        await act.Should().ThrowExactlyAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsyncReplacingTheRoleOfAnAssignmentKeepsOneRow()
    {
        await using (var db = postgres.CreateContext())
        {
            var current = await db.ActivityUserRoleAssignments.SingleAsync(
                a => a.UserId == UserId && a.ActivityId == ActivityId,
                Ct
            );
            db.ActivityUserRoleAssignments.Remove(current);
            db.ActivityUserRoleAssignments.Add(
                Assignment(ActivityId, SeedIds.ActivityRoleTypes.Volunteer)
            );
            IUnitOfWork uow = db;

            await uow.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify
            .ActivityUserRoleAssignments.Where(a =>
                a.UserId == UserId && a.ActivityId == ActivityId
            )
            .ToListAsync(Ct);
        stored
            .Should()
            .ContainSingle()
            .Which.ActivityRoleTypeId.Should()
            .Be(SeedIds.ActivityRoleTypes.Volunteer);
    }
}
