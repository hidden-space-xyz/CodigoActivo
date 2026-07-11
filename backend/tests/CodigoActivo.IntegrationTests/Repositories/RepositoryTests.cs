using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class RepositoryTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = new("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ThumbId = new("bbbbbbbb-2222-2222-2222-222222222222");

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync();

        db.Users.Add(
            new User
            {
                Id = AuthorId,
                FirstName = "Author",
                LastName = "Fixture",
                BirthDate = new DateOnly(1980, 1, 1),
                UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                UserTypeId = SeedIds.UserTypes.Member,
                CreatedAt = Fixed,
            }
        );
        db.Files.Add(
            new FileEntity
            {
                Id = ThumbId,
                Name = "thumb",
                Extension = "png",
                UploadedAt = Fixed,
                UploadedBy = AuthorId,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private CodigoActivoDbContext NewContext()
    {
        return new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }

    private static Partner NewPartner(string name = "Partner", int tier = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            FromDate = new DateOnly(2024, 1, 1),
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };

    private static User NewUser(
        string firstName = "First",
        string lastName = "Last",
        string? email = null,
        string? phone = null,
        Guid? statusId = null,
        Guid? parentId = null,
        Guid? userTypeId = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            BirthDate = new DateOnly(1990, 1, 1),
            UserStatusTypeId = statusId ?? SeedIds.UserStatusTypes.Active,
            UserTypeId = userTypeId ?? SeedIds.UserTypes.Member,
            ParentId = parentId,
            CreatedAt = Fixed,
        };

    private static Event NewEvent(string title = "Event", bool featured = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "sub",
            Description = "{}",
            EventStartsAt = new DateOnly(2026, 6, 1),
            EventEndsAt = new DateOnly(2026, 6, 2),
            Featured = featured,
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };

    private static Announcement NewAnnouncement(string title = "Ann", bool featured = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "sub",
            Description = "{}",
            Featured = featured,
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };

    private static Activity NewActivity(
        Guid eventId,
        string title = "Activity",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "d",
            Location = "loc",
            ActivityStartsAt = startsAt ?? Fixed,
            ActivityEndsAt = endsAt ?? Fixed.AddHours(1),
            EventId = eventId,
            ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };

    private static ActivityRoleType NewRoleType(string name = "Role") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "d",
        };

    private static AssignmentStatusType NewAssignmentStatus(string name = "Confirmed") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "d",
            Color = "#0f0",
        };

    [Fact]
    public async Task Query_PartnersExist_ReturnsAllRowsUntracked()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("A"), NewPartner("B"));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new PartnerRepository(ctx);

        var items = await repo.Query().ToListAsync(TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        ctx.ChangeTracker.Entries<Partner>().Should().BeEmpty("Query() uses AsNoTracking");
    }

    [Fact]
    public async Task AddAsync_BeforeSaveChanges_DoesNotPersist()
    {
        var partner = NewPartner();
        await using (var ctx = NewContext())
        {
            var repo = new PartnerRepository(ctx);
            await repo.AddAsync(partner, TestContext.Current.CancellationToken);

            await using (var probe = NewContext())
            {
                (await probe.Partners.CountAsync(TestContext.Current.CancellationToken))
                    .Should()
                    .Be(0, "the repository must not call SaveChanges");
            }

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verify = NewContext();
        (await verify.Partners.FindAsync([partner.Id], TestContext.Current.CancellationToken))
            .Should()
            .NotBeNull();
    }

    [Fact]
    public async Task FindAsync_PredicateMatch_ReturnsFirstMatchOrNull()
    {
        await using var ctx = NewContext();
        var target = NewPartner("Target");
        ctx.Partners.AddRange(target, NewPartner("Other"));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        var found = await repo.FindAsync(
            p => p.Name == "Target",
            TestContext.Current.CancellationToken
        );
        found.Should().NotBeNull();
        found!.Id.Should().Be(target.Id);
        (await repo.FindAsync(p => p.Name == "Missing", TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task GetAsync_PredicateProvided_ReturnsOnlyMatchingRows()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("Keep", tier: 5), NewPartner("Drop", tier: 1));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        var matches = await repo.GetAsync(p => p.Tier == 5, TestContext.Current.CancellationToken);

        matches.Should().ContainSingle(p => p.Name == "Keep");
    }

    [Fact]
    public async Task GetAllAsync_ThreeRowsStored_ReturnsAllRows()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(
            NewPartner("A", tier: 2),
            NewPartner("B", tier: 2),
            NewPartner("C", tier: 9)
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Should().HaveCount(3);
    }

    [Fact]
    public async Task CountAsync_PredicateProvided_CountsMatchingRows()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(
            NewPartner("A", tier: 2),
            NewPartner("B", tier: 2),
            NewPartner("C", tier: 9)
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        (await repo.CountAsync(p => p.Tier == 2, TestContext.Current.CancellationToken))
            .Should()
            .Be(2);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(99, false)]
    public async Task ExistsAsync_TierMatch_ReportsPresence(int tier, bool expected)
    {
        await using var ctx = NewContext();
        ctx.Partners.Add(NewPartner("A", tier: 9));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        (await repo.ExistsAsync(p => p.Tier == tier, TestContext.Current.CancellationToken))
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task Remove_ThenSaveChanges_DeletesEntity()
    {
        await using var ctx = NewContext();
        var partner = NewPartner();
        ctx.Partners.Add(partner);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        repo.Remove(partner);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await ctx.Partners.FindAsync([partner.Id], TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task RemoveAsync_MatchingRows_DeletesAndReturnsCount()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(
            NewPartner("X", tier: 1),
            NewPartner("Y", tier: 1),
            NewPartner("Z", tier: 8)
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        var removed = await repo.RemoveAsync(
            p => p.Tier == 1,
            TestContext.Current.CancellationToken
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        removed.Should().Be(2);
        (await ctx.Partners.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_NoRowsMatch_ReturnsZero()
    {
        await using var ctx = NewContext();
        ctx.Partners.Add(NewPartner("Only", tier: 3));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        (await repo.RemoveAsync(p => p.Tier == 100, TestContext.Current.CancellationToken))
            .Should()
            .Be(0);
        (await ctx.Partners.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_UserExists_IncludesStatusAndType()
    {
        await using var ctx = NewContext();
        var user = NewUser("Ada", "Admin");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var result = await repo.GetByIdWithDetailsAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();
        result!.UserStatusType.Name.Should().Be("Activo");
        result.UserType.Name.Should().Be("Socio");
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_UserMissing_ReturnsNull()
    {
        await using var ctx = NewContext();
        var repo = new UserRepository(ctx);

        (await repo.GetByIdWithDetailsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Theory]
    [InlineData("user@x.test")]
    [InlineData("+34600000000")]
    public async Task GetByEmailOrPhoneAsync_EmailOrPhoneIdentifier_ReturnsTheUser(
        string identifier
    )
    {
        await using var ctx = NewContext();
        var user = NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailOrPhoneAsync(
            identifier,
            TestContext.Current.CancellationToken
        );

        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmailOrPhoneAsync_UnknownIdentifier_ReturnsNull()
    {
        await using var ctx = NewContext();
        ctx.Users.Add(NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000"));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailOrPhoneAsync(
            "nobody@x.test",
            TestContext.Current.CancellationToken
        );

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("dup@x.test", true)]
    [InlineData("free@x.test", false)]
    public async Task EmailExistsAsync_NoExcludeUserId_ReportsPresence(string email, bool expected)
    {
        await using var ctx = NewContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync(email, ct: TestContext.Current.CancellationToken))
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task EmailExistsAsync_ExcludeUserIdMatchesOwner_ReturnsFalse()
    {
        await using var ctx = NewContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (
            await repo.EmailExistsAsync(
                "dup@x.test",
                excludeUserId: user.Id,
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse("owner is excluded");
    }

    [Fact]
    public async Task EmailExistsAsync_ExcludeUserIdIsOtherUser_ReturnsTrue()
    {
        await using var ctx = NewContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (
            await repo.EmailExistsAsync(
                "dup@x.test",
                excludeUserId: Guid.NewGuid(),
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeTrue("another user still collides");
    }

    [Theory]
    [InlineData("+100", true)]
    [InlineData("+999", false)]
    public async Task PhoneExistsAsync_NoExcludeUserId_ReportsPresence(string phone, bool expected)
    {
        await using var ctx = NewContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync(phone, ct: TestContext.Current.CancellationToken))
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task PhoneExistsAsync_ExcludeUserIdMatchesOwner_ReturnsFalse()
    {
        await using var ctx = NewContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (
            await repo.PhoneExistsAsync(
                "+100",
                excludeUserId: user.Id,
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse("owner is excluded");
    }

    [Fact]
    public async Task PhoneExistsAsync_ExcludeUserIdIsOtherUser_ReturnsTrue()
    {
        await using var ctx = NewContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (
            await repo.PhoneExistsAsync(
                "+100",
                excludeUserId: Guid.NewGuid(),
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeTrue("another user still collides");
    }

    [Fact]
    public async Task ListChildrenWithDetailsAsync_ParentHasChildren_ReturnsOrderedWithDetails()
    {
        await using var ctx = NewContext();
        var parent = NewUser("Parent", "P");
        var zoe = NewUser(
            "Zoe",
            "Child",
            statusId: SeedIds.UserStatusTypes.Dependent,
            parentId: parent.Id,
            userTypeId: SeedIds.UserTypes.Participant
        );
        var amy = NewUser(
            "Amy",
            "Child",
            statusId: SeedIds.UserStatusTypes.Dependent,
            parentId: parent.Id,
            userTypeId: SeedIds.UserTypes.Participant
        );
        var stranger = NewUser("Stranger", "S");
        ctx.AddRange(parent, zoe, amy, stranger);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var children = await repo.ListChildrenWithDetailsAsync(
            parent.Id,
            TestContext.Current.CancellationToken
        );

        children.Select(c => c.FirstName).Should().Equal("Amy", "Zoe");
        children[0].UserStatusType.Name.Should().Be("Dependiente");
    }

    [Fact]
    public async Task GetForEditAsync_EventWithCategories_IncludesCategoriesOrReturnsNull()
    {
        await using var ctx = NewContext();
        var category = new EventCategoryType
        {
            Id = Guid.NewGuid(),
            Name = "Cat",
            Color = "#111",
        };
        var ev = NewEvent();
        ctx.AddRange(category, ev);
        ctx.EventCategories.Add(
            new EventCategory { EventId = ev.Id, EventCategoryTypeId = category.Id }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetForEditAsync(ev.Id, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Categories.Should().ContainSingle();
        (await repo.GetForEditAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task GetWithActivitiesAndAssignmentsAsync_EventHasActivities_LoadsActivityGraph()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = activity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetWithActivitiesAndAssignmentsAsync(
            ev.Id,
            TestContext.Current.CancellationToken
        );

        loaded.Should().NotBeNull();
        loaded!.Activities.Should().ContainSingle();
        loaded.Activities.Single().Assignments.Should().ContainSingle();
        (
            await repo.GetWithActivitiesAndAssignmentsAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeNull();
    }

    [Theory]
    [InlineData(10, 50, false)]
    [InlineData(-5, 30, true)]
    [InlineData(10, 60, true)]
    [InlineData(10, 120, true)]
    public async Task AnyOutsideRangeAsync_ActivityOutsideWindow_DetectsOutOfRange(
        int startOffsetMinutes,
        int endOffsetMinutes,
        bool expected
    )
    {
        await using var ctx = NewContext();
        var ev = NewEvent();
        var lower = Fixed;
        var upper = Fixed.AddMinutes(60);
        ctx.Events.Add(ev);
        ctx.Activities.Add(
            NewActivity(
                ev.Id,
                startsAt: Fixed.AddMinutes(startOffsetMinutes),
                endsAt: Fixed.AddMinutes(endOffsetMinutes)
            )
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityRepository(ctx);

        (
            await repo.AnyOutsideRangeAsync(
                ev.Id,
                lower,
                upper,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task AnyOutsideRangeAsync_ActivityBelongsToOtherEvent_IgnoresIt()
    {
        await using var ctx = NewContext();
        var target = NewEvent("Target");
        var other = NewEvent("Other");
        ctx.Events.AddRange(target, other);
        ctx.Activities.Add(NewActivity(other.Id, startsAt: Fixed.AddMinutes(-120), endsAt: Fixed));
        ctx.Activities.Add(
            NewActivity(target.Id, startsAt: Fixed.AddMinutes(10), endsAt: Fixed.AddMinutes(50))
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityRepository(ctx);

        (
            await repo.AnyOutsideRangeAsync(
                target.Id,
                Fixed,
                Fixed.AddMinutes(60),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task GetAssignmentAsync_AssignmentExistsOrNot_ReturnsWithIncludesOrNull()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType("Ayudante");
        var status = NewAssignmentStatus("Pending");
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = activity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var found = await repo.GetAssignmentAsync(
            user.Id,
            activity.Id,
            TestContext.Current.CancellationToken
        );

        found.Should().NotBeNull();
        found!.ActivityRoleType.Name.Should().Be("Ayudante");
        found.AssignmentStatus.Name.Should().Be("Pending");
        (
            await repo.GetAssignmentAsync(
                user.Id,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task AddAssignmentAsync_BeforeSaveChanges_StagesThenPersistsOnSave()
    {
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        var assignment = new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        };

        await using (var ctx = NewContext())
        {
            ctx.AddRange(user, role, status, ev, activity);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            var repo = new ActivityRepository(ctx);

            await repo.AddAssignmentAsync(assignment, TestContext.Current.CancellationToken);
            await using (var probe = NewContext())
            {
                (
                    await probe.ActivityUserRoleAssignments.CountAsync(
                        TestContext.Current.CancellationToken
                    )
                )
                    .Should()
                    .Be(0);
            }

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verify = NewContext();
        (await verify.ActivityUserRoleAssignments.CountAsync(TestContext.Current.CancellationToken))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task RemoveAssignment_ThenSaveChanges_DeletesRow()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        var assignment = new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        };
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(assignment);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityRepository(ctx);

        repo.RemoveAssignment(assignment);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await ctx.ActivityUserRoleAssignments.CountAsync(TestContext.Current.CancellationToken))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task GetUserAssignmentsAsync_UserHasAssignments_ReturnsOrderedByActivityStart()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var other = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var late = NewActivity(ev.Id, "Late", startsAt: Fixed.AddDays(2));
        var early = NewActivity(ev.Id, "Early", startsAt: Fixed.AddDays(1));
        var foreign = NewActivity(ev.Id, "Foreign", startsAt: Fixed);
        ctx.AddRange(user, other, role, status, ev, late, early, foreign);
        ctx.ActivityUserRoleAssignments.AddRange(
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = late.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            },
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = early.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            },
            new ActivityUserRoleAssignment
            {
                UserId = other.Id,
                ActivityId = foreign.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var assignments = await repo.GetUserAssignmentsAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        assignments.Should().HaveCount(2);
        assignments.Select(a => a.Activity.Title).Should().Equal("Early", "Late");
    }

    [Fact]
    public async Task GetAssignmentsForUsersByEventAsync_EventAndUserSetProvided_FiltersAssignments()
    {
        await using var ctx = NewContext();
        var wanted = NewUser();
        var alsoWanted = NewUser();
        var excludedUser = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var targetEvent = NewEvent("Target");
        var otherEvent = NewEvent("Other");
        var targetActivity = NewActivity(targetEvent.Id);
        var otherActivity = NewActivity(otherEvent.Id);
        ctx.AddRange(
            wanted,
            alsoWanted,
            excludedUser,
            role,
            status,
            targetEvent,
            otherEvent,
            targetActivity,
            otherActivity
        );
        ctx.ActivityUserRoleAssignments.AddRange(
            new ActivityUserRoleAssignment
            {
                UserId = wanted.Id,
                ActivityId = targetActivity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            },
            new ActivityUserRoleAssignment
            {
                UserId = excludedUser.Id,
                ActivityId = targetActivity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            },
            new ActivityUserRoleAssignment
            {
                UserId = alsoWanted.Id,
                ActivityId = otherActivity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var result = await repo.GetAssignmentsForUsersByEventAsync(
            [wanted.Id, alsoWanted.Id],
            targetEvent.Id,
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result.Single().UserId.Should().Be(wanted.Id);
        result.Single().ActivityRoleType.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryAssignments_AssignmentsExist_ExposesAllRowsUntracked()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = activity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var count = await repo.QueryAssignments().CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(1);
        ctx.ChangeTracker.Entries<ActivityUserRoleAssignment>().Should().BeEmpty();
    }

    [Fact]
    public async Task IsInUseAsync_ThumbnailOrDescriptionReference_DetectsUsage()
    {
        await using var ctx = NewContext();

        var eventEmbeddedFileId = Guid.NewGuid();
        var announcementEmbeddedFileId = Guid.NewGuid();

        var ev = NewEvent();
        ev.Description = $"{{\"img\":\"/api/files/{eventEmbeddedFileId}/content\"}}";
        var announcement = NewAnnouncement();
        announcement.Description =
            $"{{\"img\":\"https://api.example.org/api/files/{announcementEmbeddedFileId}/content\"}}";
        ctx.AddRange(ev, announcement);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new FileRepository(ctx);

        (await repo.IsInUseAsync(ThumbId, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await repo.IsInUseAsync(eventEmbeddedFileId, TestContext.Current.CancellationToken))
            .Should()
            .BeTrue();
        (await repo.IsInUseAsync(announcementEmbeddedFileId, TestContext.Current.CancellationToken))
            .Should()
            .BeTrue();
        (await repo.IsInUseAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Should()
            .BeFalse();
    }
}
