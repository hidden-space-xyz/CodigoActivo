using AwesomeAssertions;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Activities;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.EventCategories;
using CodigoActivo.Infrastructure.Events;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.Infrastructure.News;
using CodigoActivo.Infrastructure.Partners;
using CodigoActivo.Infrastructure.TermsDocuments;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class RepositoryTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = new("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ThumbId = new("bbbbbbbb-2222-2222-2222-222222222222");

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);

        db.Users.Add(
            Persisted.As<User>(
                new
                {
                    Id = AuthorId,
                    FirstName = "Author",
                    LastName = "Fixture",
                    BirthDate = new DateOnly(1980, 1, 1),
                    Gender = Gender.Other,
                    Status = UserStatus.Active,
                    UserType = UserType.Member,
                    CreatedAt = Fixed,
                }
            )
        );
        db.Files.Add(
            Persisted.As<StoredFile>(
                new
                {
                    Id = ThumbId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = AuthorId,
                }
            )
        );
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static Partner NewPartner(string name = "Partner", int tier = 1)
    {
        return Partner.Create(
            new PartnerDetails(
                name,
                new DateOnly(2024, 1, 1),
                tier,
                null,
                StoredFileId.From(ThumbId)
            ),
            UserId.From(AuthorId),
            Fixed
        );
    }

    private static User NewUser(
        string firstName = "First",
        string lastName = "Last",
        string? email = null,
        string? phone = null,
        Guid? statusId = null,
        Guid? parentId = null,
        Guid? userTypeId = null
    )
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                BirthDate = new DateOnly(1990, 1, 1),
                Status = CatalogIds.UserStatuses.ValueOf(
                    statusId ?? KnownIds.UserStatusTypes.Active
                ),
                UserType = CatalogIds.UserTypes.ValueOf(userTypeId ?? KnownIds.UserTypes.Member),
                ParentId = parentId,
                CreatedAt = Fixed,
            }
        );
    }

    private static Event NewEvent(
        string title = "Event",
        bool featured = false,
        string description = "{}"
    )
    {
        return Persisted.As<Event>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Subtitle = "sub",
                Description = description,
                EventStartsAt = new DateOnly(2026, 6, 1),
                EventEndsAt = new DateOnly(2026, 6, 2),
                Featured = featured,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
    }

    private static NewsItem NewNewsItem(
        string title = "Ann",
        bool featured = false,
        string description = "{}"
    )
    {
        return Persisted.As<NewsItem>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Subtitle = "sub",
                Description = description,
                Featured = featured,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
    }

    private static Resource NewResource(string title = "Resource", string description = "{}")
    {
        return Persisted.As<Resource>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Subtitle = "sub",
                Description = description,
                ResourceType = ResourceType.Internal,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
    }

    private static Activity NewActivity(
        Guid eventId,
        string title = "Activity",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null
    )
    {
        return Persisted.As<Activity>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = "d",
                Location = "loc",
                ActivityStartsAt = startsAt ?? Fixed,
                ActivityEndsAt = endsAt ?? Fixed.AddHours(1),
                EventId = eventId,
                Modality = ActivityModality.Presencial,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
    }

    [Fact]
    public async Task AddAsyncBeforeSaveChangesDoesNotPersist()
    {
        var partner = NewPartner();
        await using (var ctx = postgres.CreateContext())
        {
            var repo = new PartnerRepository(ctx);
            await repo.AddAsync(partner, Ct);

            await using (var probe = postgres.CreateContext())
            {
                (await probe.Partners.CountAsync(Ct))
                    .Should()
                    .Be(0, "the repository must not call SaveChanges");
            }

            await ctx.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        (await verify.Partners.FindAsync([partner.Id], Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsyncPartnerExistsReturnsTrackedPartnerOrNull()
    {
        await using var ctx = postgres.CreateContext();
        var target = NewPartner("Target");
        ctx.Partners.AddRange(target, NewPartner("Other"));
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new PartnerRepository(ctx);

        var found = await repo.GetByIdAsync(target.Id, Ct);

        found.Should().NotBeNull();
        found.Name.Should().Be("Target");
        ctx.ChangeTracker.Entries<Partner>().Should().ContainSingle();
        (await repo.GetByIdAsync(PartnerId.From(Guid.NewGuid()), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveThenSaveChangesDeletesEntity()
    {
        await using var ctx = postgres.CreateContext();
        var partner = NewPartner();
        ctx.Partners.Add(partner);
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        repo.Remove(partner);
        await ctx.SaveChangesAsync(Ct);

        (await ctx.Partners.FindAsync([partner.Id], Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsyncUserExistsReturnsTheStoredAccount()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Ada", "Admin");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var result = await repo.GetByIdAsync(user.Id, Ct);

        result.Should().NotBeNull();
        result.Status.Should().Be(UserStatus.Active);
        result.UserType.Should().Be(UserType.Member);
    }

    [Fact]
    public async Task GetByIdAsyncUserMissingReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new UserRepository(ctx);

        (await repo.GetByIdAsync(UserId.From(Guid.NewGuid()), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsyncKnownEmailReturnsTheUser()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailAsync(EmailAddress.FromStored("user@x.test"), Ct);

        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmailAsyncUnknownEmailReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Users.Add(NewUser("Match", "Me", email: "user@x.test"));
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailAsync(EmailAddress.FromStored("nobody@x.test"), Ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsyncUsersSharingPhoneAndNationalIdArePersisted()
    {
        await using var ctx = postgres.CreateContext();
        var first = NewUser(email: "first@x.test", phone: "+34600000000");
        var second = NewUser(email: "second@x.test", phone: "+34600000000");
        Persisted.Overwrite(first, new { NationalId = "12345678Z" });
        Persisted.Overwrite(second, new { NationalId = "12345678Z" });
        ctx.Users.AddRange(first, second);

        await ctx.SaveChangesAsync(Ct);

        (
            await ctx
                .Users.AsNoTracking()
                .CountAsync(
                    u =>
                        u.Phone == PhoneNumber.FromStored("+34600000000")
                        && u.NationalId == SpanishNationalId.FromStored("12345678Z"),
                    Ct
                )
        )
            .Should()
            .Be(2);
    }

    [Theory]
    [InlineData("dup@x.test", true)]
    [InlineData("free@x.test", false)]
    public async Task EmailExistsAsyncNoExcludeUserIdReportsPresence(string email, bool expected)
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync(EmailAddress.FromStored(email), ct: Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task EmailExistsAsyncExcludeUserIdMatchesOwnerReturnsFalse()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (
            await repo.EmailExistsAsync(
                EmailAddress.FromStored("dup@x.test"),
                excludeUserId: user.Id,
                ct: Ct
            )
        )
            .Should()
            .BeFalse("owner is excluded");
    }

    [Fact]
    public async Task EmailExistsAsyncExcludeUserIdIsOtherUserReturnsTrue()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (
            await repo.EmailExistsAsync(
                EmailAddress.FromStored("dup@x.test"),
                excludeUserId: UserId.From(Guid.NewGuid()),
                ct: Ct
            )
        )
            .Should()
            .BeTrue("another user still collides");
    }

    [Fact]
    public async Task ListByIdsAsyncReturnsOnlyTheRequestedAccounts()
    {
        await using var ctx = postgres.CreateContext();
        var ada = NewUser("Ada", "A");
        var bob = NewUser("Bob", "B");
        var stranger = NewUser("Stranger", "S");
        ctx.AddRange(ada, bob, stranger);
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var users = await repo.ListByIdsAsync([ada.Id, bob.Id, UserId.From(Guid.NewGuid())], Ct);

        users.Select(u => u.Id).Should().BeEquivalentTo([ada.Id, bob.Id]);
    }

    [Fact]
    public async Task LockPasswordStateAsyncTakesTheCountersAnotherAttemptSaved()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Ada", "A");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        await using (var other = postgres.CreateContext())
        {
            var otherRepo = new UserRepository(other);
            var copy = await otherRepo.GetByIdAsync(user.Id, Ct);
            copy!.RecordPasswordFailure(5, Fixed);
            await otherRepo.SavePasswordStateAsync(copy, Ct);
        }

        var repo = new UserRepository(ctx);
        await using var transaction = await ctx.Database.BeginTransactionAsync(Ct);

        (await repo.LockPasswordStateAsync(user, Ct)).Should().BeTrue();

        user.PasswordFailedAttempts.Should().Be(1);
        ctx.Entry(user).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task LockPasswordStateAsyncAccountNotStoredReturnsFalse()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Ada", "A");
        var repo = new UserRepository(ctx);
        await using var transaction = await ctx.Database.BeginTransactionAsync(Ct);

        (await repo.LockPasswordStateAsync(user, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsyncEventWithCategoriesIncludesCategoriesOrReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        var category = Persisted.As<EventCategoryType>(
            new
            {
                Id = Guid.NewGuid(),
                Name = "Cat",
                Color = "#111",
            }
        );
        var ev = NewEvent();
        ctx.AddRange(category, ev);
        ctx.EventCategories.Add(
            Persisted.As<EventCategory>(new { EventId = ev.Id, EventCategoryTypeId = category.Id })
        );
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetByIdAsync(ev.Id, Ct);

        loaded.Should().NotBeNull();
        loaded.Categories.Should().ContainSingle();
        (await repo.GetByIdAsync(EventId.From(Guid.NewGuid()), Ct)).Should().BeNull();
    }

    [Theory]
    [InlineData(10, 50, false)]
    [InlineData(-5, 30, true)]
    [InlineData(10, 60, true)]
    [InlineData(10, 120, true)]
    public async Task AnyOutsideRangeAsyncActivityOutsideWindowDetectsOutOfRange(
        int startOffsetMinutes,
        int endOffsetMinutes,
        bool expected
    )
    {
        await using var ctx = postgres.CreateContext();
        var ev = NewEvent();
        var lower = Fixed;
        var upper = Fixed.AddMinutes(60);
        ctx.Events.Add(ev);
        ctx.Activities.Add(
            NewActivity(
                ev.Id.Value,
                startsAt: Fixed.AddMinutes(startOffsetMinutes),
                endsAt: Fixed.AddMinutes(endOffsetMinutes)
            )
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(ev.Id, lower, upper, Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task AnyOutsideRangeAsyncActivityBelongsToOtherEventIgnoresIt()
    {
        await using var ctx = postgres.CreateContext();
        var target = NewEvent("Target");
        var other = NewEvent("Other");
        ctx.Events.AddRange(target, other);
        ctx.Activities.Add(
            NewActivity(other.Id.Value, startsAt: Fixed.AddMinutes(-120), endsAt: Fixed)
        );
        ctx.Activities.Add(
            NewActivity(
                target.Id.Value,
                startsAt: Fixed.AddMinutes(10),
                endsAt: Fixed.AddMinutes(50)
            )
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(target.Id, Fixed, Fixed.AddMinutes(60), Ct))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task GetByIdAsyncActivityWithSignupsLoadsCapacitiesAndSignupsOrReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id.Value);
        ctx.AddRange(user, ev, activity);
        ctx.ActivityRoleCapacities.Add(
            Persisted.As<ActivityRoleCapacity>(
                new
                {
                    ActivityId = activity.Id,
                    Role = ActivityRole.Participant,
                    DesiredCount = 3,
                }
            )
        );
        ctx.Assignments.Add(
            Persisted.As<Assignment>(
                new
                {
                    UserId = user.Id,
                    ActivityId = activity.Id,
                    Role = ActivityRole.Volunteer,
                    Status = AssignmentStatus.Confirmed,
                }
            )
        );
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var found = await repo.GetByIdAsync(activity.Id, Ct);

        found.Should().NotBeNull();
        found.RoleCapacities.Should().ContainSingle().Which.DesiredCount.Should().Be(3);
        found.AssignmentOf(user.Id)!.Role.Should().Be(ActivityRole.Volunteer);
        (await repo.GetByIdAsync(ActivityId.From(Guid.NewGuid()), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task RequestAssignmentThenSaveChangesPersistsARequestedSignup()
    {
        var user = NewUser();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id.Value);
        await using (var seed = postgres.CreateContext())
        {
            seed.AddRange(user, ev, activity);
            await seed.SaveChangesAsync(Ct);
        }

        await using (var ctx = postgres.CreateContext())
        {
            var loaded = await new ActivityRepository(ctx).GetByIdAsync(activity.Id, Ct);
            loaded!
                .RequestAssignment(user.Id, ActivityRole.Participant, Fixed)
                .IsSuccess.Should()
                .BeTrue();
            await ctx.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Assignments.SingleAsync(Ct);
        stored.UserId.Should().Be(user.Id);
        stored.Status.Should().Be(AssignmentStatus.Requested);
        stored.CreatedAt.Should().Be(Fixed);
    }

    [Fact]
    public async Task ChangeAssignmentRoleThenSaveChangesReplacesTheRowKeepingStatusAndDate()
    {
        var user = NewUser();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id.Value);
        await using (var seed = postgres.CreateContext())
        {
            seed.AddRange(user, ev, activity);
            seed.Assignments.Add(
                Persisted.As<Assignment>(
                    new
                    {
                        UserId = user.Id,
                        ActivityId = activity.Id,
                        Role = ActivityRole.Participant,
                        Status = AssignmentStatus.Confirmed,
                        CreatedAt = Fixed,
                    }
                )
            );
            await seed.SaveChangesAsync(Ct);
        }

        await using (var ctx = postgres.CreateContext())
        {
            var loaded = await new ActivityRepository(ctx).GetByIdAsync(activity.Id, Ct);
            loaded!.ChangeAssignmentRole(user.Id, ActivityRole.Volunteer).Should().BeTrue();
            await ctx.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        var stored = await verify.Assignments.SingleAsync(Ct);
        stored.Role.Should().Be(ActivityRole.Volunteer);
        stored.Status.Should().Be(AssignmentStatus.Confirmed);
        stored.CreatedAt.Should().Be(Fixed);
    }

    [Fact]
    public async Task UnassignThenSaveChangesDeletesTheSignup()
    {
        var user = NewUser();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id.Value);
        await using (var seed = postgres.CreateContext())
        {
            seed.AddRange(user, ev, activity);
            seed.Assignments.Add(
                Persisted.As<Assignment>(
                    new
                    {
                        UserId = user.Id,
                        ActivityId = activity.Id,
                        Role = ActivityRole.Participant,
                        Status = AssignmentStatus.Requested,
                    }
                )
            );
            await seed.SaveChangesAsync(Ct);
        }

        await using (var ctx = postgres.CreateContext())
        {
            var loaded = await new ActivityRepository(ctx).GetByIdAsync(activity.Id, Ct);
            loaded!.Unassign(user.Id).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        (await verify.Assignments.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task HasConfirmedAttendanceAsyncDependentConfirmedCountsForTheGuardian()
    {
        await using var ctx = postgres.CreateContext();
        var guardian = NewUser("Tutora");
        var child = NewUser("Menor", parentId: guardian.Id.Value);
        var ev = NewEvent();
        var otherEvent = NewEvent("Otro");
        var activity = NewActivity(ev.Id.Value);
        ctx.AddRange(guardian, child, ev, otherEvent, activity);
        ctx.Assignments.Add(
            Persisted.As<Assignment>(
                new
                {
                    UserId = child.Id,
                    ActivityId = activity.Id,
                    Role = ActivityRole.Participant,
                    Status = AssignmentStatus.Confirmed,
                }
            )
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.HasConfirmedAttendanceAsync(ev.Id, guardian.Id, Ct)).Should().BeTrue();
        (await repo.HasConfirmedAttendanceAsync(ev.Id, child.Id, Ct)).Should().BeTrue();
        (await repo.HasConfirmedAttendanceAsync(otherEvent.Id, guardian.Id, Ct)).Should().BeFalse();
        (await repo.HasConfirmedAttendanceAsync(ev.Id, UserId.From(AuthorId), Ct))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task ListThumbnailIdsAsyncEventWithActivitiesListsTheirThumbnails()
    {
        await using var ctx = postgres.CreateContext();
        var ev = NewEvent();
        ctx.AddRange(ev, NewActivity(ev.Id.Value, "Una"), NewActivity(ev.Id.Value, "Otra"));
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.ListThumbnailIdsAsync(ev.Id, Ct))
            .Should()
            .Equal(StoredFileId.From(ThumbId), StoredFileId.From(ThumbId));
    }

    [Fact]
    public async Task IsInUseAsyncThumbnailOrDescriptionReferenceDetectsUsage()
    {
        await using var ctx = postgres.CreateContext();

        var eventEmbeddedFileId = Guid.NewGuid();
        var newsItemEmbeddedFileId = Guid.NewGuid();

        var ev = NewEvent(description: $"{{\"img\":\"/api/files/{eventEmbeddedFileId}/content\"}}");
        var newsItem = NewNewsItem(
            description: $"{{\"img\":\"https://api.example.org/api/files/{newsItemEmbeddedFileId}/content\"}}"
        );
        ctx.AddRange(ev, newsItem);
        await ctx.SaveChangesAsync(Ct);
        var repo = new StoredFileRepository(ctx);

        (await repo.IsInUseAsync(StoredFileId.From(ThumbId), Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(StoredFileId.From(eventEmbeddedFileId), Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(StoredFileId.From(newsItemEmbeddedFileId), Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(StoredFileId.From(Guid.NewGuid()), Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task GetInUseAsyncMixedReferencesReturnsOnlyReferencedCandidates()
    {
        await using var ctx = postgres.CreateContext();

        var embeddedInEventId = Guid.NewGuid();
        var embeddedInNewsItemId = Guid.NewGuid();
        var embeddedInResourceId = Guid.NewGuid();
        var embeddedButNotCandidateId = Guid.NewGuid();
        var unreferencedId = Guid.NewGuid();

        var ev = NewEvent(
            description: $"{{\"a\":\"/api/files/{embeddedInEventId}/content\","
                + $"\"b\":\"/api/files/{embeddedButNotCandidateId}/content\"}}"
        );
        var newsItem = NewNewsItem(
            description: $"{{\"img\":\"https://api.example.org/api/files/{embeddedInNewsItemId}/content\"}}"
        );
        var resource = NewResource(
            description: $"{{\"img\":\"/api/files/{embeddedInResourceId}/content\"}}"
        );
        ctx.AddRange(ev, newsItem, resource);
        await ctx.SaveChangesAsync(Ct);
        var repo = new StoredFileRepository(ctx);

        var inUse = await repo.GetInUseAsync(
            [
                StoredFileId.From(ThumbId),
                StoredFileId.From(embeddedInEventId),
                StoredFileId.From(embeddedInNewsItemId),
                StoredFileId.From(embeddedInResourceId),
                StoredFileId.From(unreferencedId),
            ],
            Ct
        );

        inUse
            .Select(id => id.Value)
            .Should()
            .BeEquivalentTo([
                ThumbId,
                embeddedInEventId,
                embeddedInNewsItemId,
                embeddedInResourceId,
            ]);
    }

    [Fact]
    public async Task GetInUseAsyncEmptyInputReturnsEmpty()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new StoredFileRepository(ctx);

        var inUse = await repo.GetInUseAsync([], Ct);

        inUse.Should().BeEmpty();
    }

    [Fact]
    public async Task NameExistsAsyncEventCategoryTypeRenamedIgnoresItselfButNotOthers()
    {
        await using var ctx = postgres.CreateContext();
        var talleres = EventCategoryType.Create("Talleres", "#123456");
        var charlas = EventCategoryType.Create("Charlas", "#654321");
        ctx.EventCategoryTypes.AddRange(talleres, charlas);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventCategoryTypeRepository(ctx);

        (await repo.NameExistsAsync("Talleres", ct: Ct)).Should().BeTrue();
        (await repo.NameExistsAsync("Talleres", talleres.Id, Ct)).Should().BeFalse();
        (await repo.NameExistsAsync("Charlas", talleres.Id, Ct)).Should().BeTrue();
        (await repo.NameExistsAsync("Otra", ct: Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task CountExistingAsyncEventCategoryTypesCountsOnlyStoredOnes()
    {
        await using var ctx = postgres.CreateContext();
        var stored = EventCategoryType.Create("Efímera", "#123456");
        ctx.EventCategoryTypes.Add(stored);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventCategoryTypeRepository(ctx);

        (await repo.CountExistingAsync([stored.Id, EventCategoryTypeId.From(Guid.NewGuid())], Ct))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task ListFeaturedAsyncEventsStoredReturnsOnlyTheFeaturedOnes()
    {
        await using var ctx = postgres.CreateContext();
        var featured = NewEvent("Destacado", featured: true);
        ctx.Events.AddRange(featured, NewEvent("Normal"));
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var events = await repo.ListFeaturedAsync(Ct);

        events.Select(e => e.Id).Should().Equal(featured.Id);
    }

    [Fact]
    public async Task LinksTermsDocumentAsyncEventLinksDocumentReportsIt()
    {
        await using var ctx = postgres.CreateContext();
        var linked = TermsDocument.Create("Vinculado", RichText.From("{}"));
        var unlinked = TermsDocument.Create("Suelto", RichText.From("{}"));
        var ev = NewEvent();
        Persisted.Add(
            ev.TermsDocuments,
            Persisted.As<EventTermsDocument>(
                new
                {
                    TermsDocumentId = linked.Id,
                    IsRequired = true,
                    DisplayOrder = 0,
                }
            )
        );
        ctx.AddRange(linked, unlinked, ev);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventRepository(ctx);

        (await repo.LinksTermsDocumentAsync(linked.Id, Ct)).Should().BeTrue();
        (await repo.LinksTermsDocumentAsync(unlinked.Id, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task ListAsyncTermsAcceptancesReturnsOnlyTheDecisionsOfTheUserForTheEvent()
    {
        await using var ctx = postgres.CreateContext();
        var document = TermsDocument.Create("Normas del listado", RichText.From("{}"));
        var user = NewUser("Ada", "A");
        var other = NewUser("Bob", "B");
        var ev = NewEvent();
        var otherEvent = NewEvent("Otro evento");
        ctx.AddRange(document, user, other, ev, otherEvent);
        ctx.AddRange(
            EventTermsAcceptance.Record(ev.Id, user.Id, document.Id, true, Fixed),
            EventTermsAcceptance.Record(ev.Id, other.Id, document.Id, false, Fixed),
            EventTermsAcceptance.Record(otherEvent.Id, user.Id, document.Id, false, Fixed)
        );
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new EventTermsAcceptanceRepository(ctx);

        var decisions = await repo.ListAsync(ev.Id, user.Id, Ct);

        decisions.Should().ContainSingle().Which.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task AnyForDocumentAsyncDecisionOnTheDocumentReportsIt()
    {
        await using var ctx = postgres.CreateContext();
        var decided = TermsDocument.Create("Documento decidido", RichText.From("{}"));
        var undecided = TermsDocument.Create("Documento sin decisiones", RichText.From("{}"));
        var user = NewUser("Ada", "A");
        var ev = NewEvent();
        ctx.AddRange(decided, undecided, user, ev);
        ctx.Add(EventTermsAcceptance.Record(ev.Id, user.Id, decided.Id, false, Fixed));
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventTermsAcceptanceRepository(ctx);

        (await repo.AnyForDocumentAsync(decided.Id, Ct)).Should().BeTrue();
        (await repo.AnyForDocumentAsync(undecided.Id, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task CountExistingAsyncTermsDocumentsCountsOnlyStoredOnes()
    {
        await using var ctx = postgres.CreateContext();
        var stored = TermsDocument.Create("Documento contado", RichText.From("{}"));
        ctx.TermsDocuments.Add(stored);
        await ctx.SaveChangesAsync(Ct);
        var repo = new TermsDocumentRepository(ctx);

        (await repo.CountExistingAsync([stored.Id, TermsDocumentId.From(Guid.NewGuid())], Ct))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task ListFeaturedAsyncNewsItemsStoredReturnsOnlyTheFeaturedOnes()
    {
        await using var ctx = postgres.CreateContext();
        var featured = NewNewsItem("Destacada", featured: true);
        ctx.News.AddRange(featured, NewNewsItem("Normal"));
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new NewsItemRepository(ctx);

        var items = await repo.ListFeaturedAsync(Ct);

        items.Select(item => item.Id).Should().Equal(featured.Id);
    }
}
