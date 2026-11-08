using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class UnitOfWorkTests(PostgresDatabaseFixture postgres)
    : IClassFixture<PostgresDatabaseFixture>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private static readonly Guid UserId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid FileId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid EventId = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid ActivityId = new("cccccccc-0000-0000-0000-000000000004");
    private const string UserEmail = "unit-of-work@codigoactivo.test";

    private static readonly global::CodigoActivo.Domain.Events.EventId SeededEventId =
        global::CodigoActivo.Domain.Events.EventId.From(EventId);

    private static readonly global::CodigoActivo.Domain.Users.UserId SeededUserId =
        global::CodigoActivo.Domain.Users.UserId.From(UserId);

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.DeleteAllRowsAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);

        db.Users.Add(NewUser(UserId, UserEmail));
        db.Files.Add(
            Persisted.As<StoredFile>(
                new
                {
                    Id = FileId,
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

    private static TermsDocument NewTermsDocument(string name)
    {
        return TermsDocument.Create(
            name,
            RichText.From("{}"),
            requiresSignature: false,
            TermsDocumentKind.Conditions
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
    public async Task SaveChangesAsyncCategoryTypeNameDifferingOnlyInCaseNamesTheCategoryTypeEntity()
    {
        await using var db = postgres.CreateContext();
        db.EventCategoryTypes.Add(EventCategoryType.Create("Astronomia", "#112233"));
        await db.SaveChangesAsync(Ct);
        db.EventCategoryTypes.Add(EventCategoryType.Create("ASTRONOMIA", "#445566"));
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<EventCategoryType>();
    }

    [Fact]
    public async Task SaveChangesAsyncTermsDocumentNameDifferingOnlyInCaseNamesTheTermsDocumentEntity()
    {
        await using var db = postgres.CreateContext();
        db.TermsDocuments.Add(NewTermsDocument("Normas del campamento"));
        await db.SaveChangesAsync(Ct);
        db.TermsDocuments.Add(NewTermsDocument("NORMAS DEL CAMPAMENTO"));
        var uow = TestUnitOfWork.For(db);

        var act = () => uow.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        thrown.Which.EntityType.Should().Be<TermsDocument>();
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
    public async Task SaveChangesAsyncChallengeOpenedWhileThePasswordChangedIsRefusedAndStoresNothingOfIt()
    {
        await using var login = postgres.CreateContext();
        var uow = TestUnitOfWork.For(login);
        var user = await login.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        await using (var reset = postgres.CreateContext())
        {
            (await reset.Users.SingleAsync(u => u.Id == SeededUserId, Ct)).ResetPassword(
                "new-hash",
                Fixed
            );
            await reset.SaveChangesAsync(Ct);
        }

        user.IssueLoginCode("code-hash", Fixed, TimeSpan.FromMinutes(10))
            .IsSuccess.Should()
            .BeTrue();
        user.StartLoginChallenge(LoginChallengeId.New(), Fixed);

        var save = () => uow.SaveChangesAsync(Ct);

        var thrown = await save.Should().ThrowAsync<ConcurrentUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordHash.Should().Be("new-hash");
        stored.LoginChallenge.Id.Should().BeNull();
        stored.LoginCode.Hash.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsyncAfterACountedPasswordFailureCommitsTheOtherChangesOfTheAccount()
    {
        await using (var db = postgres.CreateContext())
        {
            var uow = TestUnitOfWork.For(db);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;
            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    await users.LockPasswordStateAsync(user, attempt);
                    user.RecordPasswordFailure(Fixed, 5, LockoutDuration);
                    await users.SavePasswordStateAsync(user, attempt);
                    return true;
                },
                Ct
            );
            user.ClearPasswordFailures();
            user.ChangeType(UserType.Sponsor, Fixed);

            await uow.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordLockout.FailedAttempts.Should().Be(0);
        stored.UserType.Should().Be(UserType.Sponsor);
    }

    [Fact]
    public async Task SavePasswordStateAsyncAccountChangedMeanwhileStoresTheFailureAndKeepsTheConflict()
    {
        await using (var db = postgres.CreateContext())
        {
            var uow = TestUnitOfWork.For(db);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;
            await using (var concurrent = postgres.CreateContext())
            {
                (await concurrent.Users.SingleAsync(u => u.Id == SeededUserId, Ct)).ChangeType(
                    UserType.Sponsor,
                    Fixed
                );
                await concurrent.SaveChangesAsync(Ct);
            }

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    await users.LockPasswordStateAsync(user, attempt);
                    user.RecordPasswordFailure(Fixed, 5, LockoutDuration);
                    await users.SavePasswordStateAsync(user, attempt);
                    return true;
                },
                Ct
            );
            user.ChangeStaffRole(StaffRole.Administrator, Fixed);

            var save = () => uow.SaveChangesAsync(Ct);

            await save.Should().ThrowAsync<ConcurrentUpdateException>();
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordLockout.FailedAttempts.Should().Be(1);
        stored.UserType.Should().Be(UserType.Sponsor);
        stored.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task LockAsyncAccountChangedMeanwhileReloadsItsVersionSoTheLockedSaveCommits()
    {
        await using (var db = postgres.CreateContext())
        {
            var uow = TestUnitOfWork.For(db);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;
            await using (var concurrent = postgres.CreateContext())
            {
                (await concurrent.Users.SingleAsync(u => u.Id == SeededUserId, Ct)).ChangeStaffRole(
                    StaffRole.Administrator,
                    Fixed
                );
                await concurrent.SaveChangesAsync(Ct);
            }

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    await users.LockAsync(user, attempt);
                    user.DesignateAsPrivacyOfficer(Fixed);
                    return await uow.SaveChangesAsync(attempt);
                },
                Ct
            );
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.StaffRole.Should().Be(StaffRole.Administrator);
        stored.IsPrivacyOfficer.Should().BeTrue();
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
    public async Task SaveChangesAsyncRefusedCommitKeepsItsEventsForTheNextCommit()
    {
        await using var db = postgres.CreateContext();
        var publisher = new RecordingPublisher();
        var uow = new UnitOfWork(db, publisher);
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
    public async Task ExecuteInTransactionAsyncPasswordLockIsPublishedOnceTheTransactionCommits()
    {
        await using (var db = postgres.CreateContext())
        {
            var publisher = new RecordingPublisher();
            var uow = new UnitOfWork(db, publisher);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    await LockPasswordAsync(users, user, attempt);
                    publisher.Published.Should().BeEmpty();
                    return true;
                },
                Ct
            );

            publisher.Published.Should().Equal(new PasswordLocked(SeededUserId));
            (await uow.SaveChangesAsync(Ct)).Should().Be(0);
            publisher.Published.Should().ContainSingle();
        }

        await using var verify = postgres.CreateContext();
        (await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct))
            .PasswordLockout.LockedUntil.Should()
            .Be(Fixed + LockoutDuration);
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncRolledBackPasswordLockIsNeverPublished()
    {
        await using (var db = postgres.CreateContext())
        {
            var publisher = new RecordingPublisher();
            var uow = new UnitOfWork(db, publisher);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;

            var refused = () =>
                uow.ExecuteInTransactionAsync<bool>(
                    async attempt =>
                    {
                        await LockPasswordAsync(users, user, attempt);
                        throw new InvalidOperationException("refused");
                    },
                    Ct
                );

            await refused.Should().ThrowAsync<InvalidOperationException>();
            publisher.Published.Should().BeEmpty();
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordLockout.IsLocked(Fixed).Should().BeFalse();
        stored.PasswordLockout.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncDeadlockedWorkThrowsConcurrentUpdate()
    {
        await using var db = postgres.CreateContext();
        var uow = TestUnitOfWork.For(db);
        var deadlock = new InvalidOperationException("wrapped", Deadlock());

        var deadlocked = () => uow.ExecuteInTransactionAsync<bool>(_ => throw deadlock, Ct);

        (await deadlocked.Should().ThrowAsync<ConcurrentUpdateException>())
            .Which.InnerException.Should()
            .BeSameAs(deadlock);
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncPasswordLockLeavesTheOtherChangesOfTheAccountStaged()
    {
        await using (var db = postgres.CreateContext())
        {
            var publisher = new RecordingPublisher();
            var uow = new UnitOfWork(db, publisher);
            var users = new UserRepository(db, uow);
            var user = (await users.GetByIdAsync(SeededUserId, Ct))!;
            user.ChangeStaffRole(StaffRole.Administrator, Fixed);

            await uow.ExecuteInTransactionAsync(
                async attempt =>
                {
                    user.DesignateAsPrivacyOfficer(Fixed);
                    return await LockPasswordAsync(users, user, attempt);
                },
                Ct
            );

            publisher.Published.Should().Equal(new PasswordLocked(SeededUserId));
            await using (var meanwhile = postgres.CreateContext())
            {
                var committed = await meanwhile.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
                committed.PasswordLockout.IsLocked(Fixed).Should().BeTrue();
                committed.StaffRole.Should().Be(StaffRole.None);
                committed.IsPrivacyOfficer.Should().BeFalse();
            }

            await uow.SaveChangesAsync(Ct);

            publisher
                .Published.Should()
                .Equal(
                    new PasswordLocked(SeededUserId),
                    new StaffRoleChanged(SeededUserId, StaffRole.Administrator),
                    new PrivacyOfficerChanged(SeededUserId, true)
                );
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordLockout.IsLocked(Fixed).Should().BeTrue();
        stored.StaffRole.Should().Be(StaffRole.Administrator);
        stored.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task SavePasswordStateAsyncLockOutsideATransactionIsPublishedRightAway()
    {
        await using var db = postgres.CreateContext();
        var publisher = new RecordingPublisher();
        var uow = new UnitOfWork(db, publisher);
        var users = new UserRepository(db, uow);
        var user = (await users.GetByIdAsync(SeededUserId, Ct))!;
        user.ChangeType(UserType.Sponsor, Fixed);
        user.RecordPasswordFailure(Fixed, 1, LockoutDuration);

        await users.SavePasswordStateAsync(user, Ct);

        publisher.Published.Should().Equal(new PasswordLocked(SeededUserId));
        user.PullDomainEvents().Should().Equal(new UserTypeChanged(SeededUserId));
        await using var verify = postgres.CreateContext();
        var stored = await verify.Users.SingleAsync(u => u.Id == SeededUserId, Ct);
        stored.PasswordLockout.LockedUntil.Should().Be(Fixed + LockoutDuration);
        stored.UserType.Should().Be(UserType.Member);
    }

    private static async Task<bool> LockPasswordAsync(
        UserRepository users,
        User user,
        CancellationToken attempt
    )
    {
        await users.LockPasswordStateAsync(user, attempt);
        user.RecordPasswordFailure(Fixed, 1, LockoutDuration);
        await users.SavePasswordStateAsync(user, attempt);
        return true;
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

        public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents)
        {
            Published.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }
}
