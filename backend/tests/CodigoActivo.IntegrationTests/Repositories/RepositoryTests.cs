using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
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
        await new DatabaseSeeder(db).SeedAsync();

        db.Users.Add(
            new User
            {
                Id = AuthorId,
                FirstName = "Author",
                LastName = "Fixture",
                BirthDate = new DateOnly(1980, 1, 1),
                Gender = Gender.Other,
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
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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

    private static Resource NewResource(string title = "Resource") =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "sub",
            Description = "{}",
            ResourceTypeId = SeedIds.ResourceTypes.Internal,
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
        await using var ctx = postgres.CreateContext();
        ctx.Partners.AddRange(NewPartner("A"), NewPartner("B"));
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new PartnerRepository(ctx);

        var items = await repo.Query().ToListAsync(Ct);

        items.Should().HaveCount(2);
        ctx.ChangeTracker.Entries<Partner>().Should().BeEmpty("Query() uses AsNoTracking");
    }

    [Fact]
    public async Task AddAsync_BeforeSaveChanges_DoesNotPersist()
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
    public async Task FindAsync_PredicateMatch_ReturnsFirstMatchOrNull()
    {
        await using var ctx = postgres.CreateContext();
        var target = NewPartner("Target");
        ctx.Partners.AddRange(target, NewPartner("Other"));
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        var found = await repo.FindAsync(p => p.Name == "Target", Ct);
        found.Should().NotBeNull();
        found!.Id.Should().Be(target.Id);
        (await repo.FindAsync(p => p.Name == "Missing", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_PredicateProvided_ReturnsOnlyMatchingRows()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Partners.AddRange(NewPartner("Keep", tier: 5), NewPartner("Drop", tier: 1));
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        var matches = await repo.GetAsync(p => p.Tier == 5, Ct);

        matches.Should().ContainSingle(p => p.Name == "Keep");
    }

    [Fact]
    public async Task CountAsync_PredicateProvided_CountsMatchingRows()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Partners.AddRange(
            NewPartner("A", tier: 2),
            NewPartner("B", tier: 2),
            NewPartner("C", tier: 9)
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        (await repo.CountAsync(p => p.Tier == 2, Ct)).Should().Be(2);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(99, false)]
    public async Task ExistsAsync_TierMatch_ReportsPresence(int tier, bool expected)
    {
        await using var ctx = postgres.CreateContext();
        ctx.Partners.Add(NewPartner("A", tier: 9));
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        (await repo.ExistsAsync(p => p.Tier == tier, Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task Remove_ThenSaveChanges_DeletesEntity()
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
    public async Task RemoveAsync_MatchingRows_DeletesAndReturnsCount()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Partners.AddRange(
            NewPartner("X", tier: 1),
            NewPartner("Y", tier: 1),
            NewPartner("Z", tier: 8)
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        var removed = await repo.RemoveAsync(p => p.Tier == 1, Ct);
        await ctx.SaveChangesAsync(Ct);

        removed.Should().Be(2);
        (await ctx.Partners.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_NoRowsMatch_ReturnsZero()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Partners.Add(NewPartner("Only", tier: 3));
        await ctx.SaveChangesAsync(Ct);
        var repo = new PartnerRepository(ctx);

        (await repo.RemoveAsync(p => p.Tier == 100, Ct)).Should().Be(0);
        (await ctx.Partners.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_UserExists_IncludesStatus()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Ada", "Admin");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var result = await repo.GetByIdWithDetailsAsync(user.Id, Ct);

        result.Should().NotBeNull();
        result!.UserStatusType.Name.Should().Be("Activo");
        result.UserTypeId.Should().Be(SeedIds.UserTypes.Member);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_UserMissing_ReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new UserRepository(ctx);

        (await repo.GetByIdWithDetailsAsync(Guid.NewGuid(), Ct)).Should().BeNull();
    }

    [Theory]
    [InlineData("user@x.test")]
    [InlineData("+34600000000")]
    public async Task GetByEmailOrPhoneAsync_EmailOrPhoneIdentifier_ReturnsTheUser(
        string identifier
    )
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailOrPhoneAsync(identifier, Ct);

        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmailOrPhoneAsync_UnknownIdentifier_ReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
        ctx.Users.Add(NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000"));
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailOrPhoneAsync("nobody@x.test", Ct);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("dup@x.test", true)]
    [InlineData("free@x.test", false)]
    public async Task EmailExistsAsync_NoExcludeUserId_ReportsPresence(string email, bool expected)
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync(email, ct: Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task EmailExistsAsync_ExcludeUserIdMatchesOwner_ReturnsFalse()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync("dup@x.test", excludeUserId: user.Id, ct: Ct))
            .Should()
            .BeFalse("owner is excluded");
    }

    [Fact]
    public async Task EmailExistsAsync_ExcludeUserIdIsOtherUser_ReturnsTrue()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync("dup@x.test", excludeUserId: Guid.NewGuid(), ct: Ct))
            .Should()
            .BeTrue("another user still collides");
    }

    [Theory]
    [InlineData("+100", true)]
    [InlineData("+999", false)]
    public async Task PhoneExistsAsync_NoExcludeUserId_ReportsPresence(string phone, bool expected)
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync(phone, ct: Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task PhoneExistsAsync_ExcludeUserIdMatchesOwner_ReturnsFalse()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync("+100", excludeUserId: user.Id, ct: Ct))
            .Should()
            .BeFalse("owner is excluded");
    }

    [Fact]
    public async Task PhoneExistsAsync_ExcludeUserIdIsOtherUser_ReturnsTrue()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(Ct);
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync("+100", excludeUserId: Guid.NewGuid(), ct: Ct))
            .Should()
            .BeTrue("another user still collides");
    }

    [Fact]
    public async Task ListChildrenWithDetailsAsync_ParentHasChildren_ReturnsOrderedWithDetails()
    {
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var children = await repo.ListChildrenWithDetailsAsync(parent.Id, Ct);

        children.Select(c => c.FirstName).Should().Equal("Amy", "Zoe");
        children[0].UserStatusType.Name.Should().Be("Dependiente");
    }

    [Fact]
    public async Task GetForEditAsync_EventWithCategories_IncludesCategoriesOrReturnsNull()
    {
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetForEditAsync(ev.Id, Ct);

        loaded.Should().NotBeNull();
        loaded!.Categories.Should().ContainSingle();
        (await repo.GetForEditAsync(Guid.NewGuid(), Ct)).Should().BeNull();
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
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(ev.Id, lower, upper, Ct)).Should().Be(expected);
    }

    [Fact]
    public async Task AnyOutsideRangeAsync_ActivityBelongsToOtherEvent_IgnoresIt()
    {
        await using var ctx = postgres.CreateContext();
        var target = NewEvent("Target");
        var other = NewEvent("Other");
        ctx.Events.AddRange(target, other);
        ctx.Activities.Add(NewActivity(other.Id, startsAt: Fixed.AddMinutes(-120), endsAt: Fixed));
        ctx.Activities.Add(
            NewActivity(target.Id, startsAt: Fixed.AddMinutes(10), endsAt: Fixed.AddMinutes(50))
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(target.Id, Fixed, Fixed.AddMinutes(60), Ct))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task GetAssignmentAsync_AssignmentExistsOrNot_ReturnsWithIncludesOrNull()
    {
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var found = await repo.GetAssignmentAsync(user.Id, activity.Id, Ct);

        found.Should().NotBeNull();
        found!.ActivityRoleType.Name.Should().Be("Ayudante");
        found.AssignmentStatus.Name.Should().Be("Pending");
        (await repo.GetAssignmentAsync(user.Id, Guid.NewGuid(), Ct)).Should().BeNull();
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

        await using (var ctx = postgres.CreateContext())
        {
            ctx.AddRange(user, role, status, ev, activity);
            await ctx.SaveChangesAsync(Ct);
            var repo = new ActivityRepository(ctx);

            await repo.AddAssignmentAsync(assignment, Ct);
            await using (var probe = postgres.CreateContext())
            {
                (await probe.ActivityUserRoleAssignments.CountAsync(Ct)).Should().Be(0);
            }

            await ctx.SaveChangesAsync(Ct);
        }

        await using var verify = postgres.CreateContext();
        (await verify.ActivityUserRoleAssignments.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAssignment_ThenSaveChanges_DeletesRow()
    {
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        repo.RemoveAssignment(assignment);
        await ctx.SaveChangesAsync(Ct);

        (await ctx.ActivityUserRoleAssignments.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task QueryAssignments_AssignmentsExist_ExposesAllRowsUntracked()
    {
        await using var ctx = postgres.CreateContext();
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
        await ctx.SaveChangesAsync(Ct);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var count = await repo.QueryAssignments().CountAsync(Ct);

        count.Should().Be(1);
        ctx.ChangeTracker.Entries<ActivityUserRoleAssignment>().Should().BeEmpty();
    }

    [Fact]
    public async Task IsInUseAsync_ThumbnailOrDescriptionReference_DetectsUsage()
    {
        await using var ctx = postgres.CreateContext();

        var eventEmbeddedFileId = Guid.NewGuid();
        var announcementEmbeddedFileId = Guid.NewGuid();

        var ev = NewEvent();
        ev.Description = $"{{\"img\":\"/api/files/{eventEmbeddedFileId}/content\"}}";
        var announcement = NewAnnouncement();
        announcement.Description =
            $"{{\"img\":\"https://api.example.org/api/files/{announcementEmbeddedFileId}/content\"}}";
        ctx.AddRange(ev, announcement);
        await ctx.SaveChangesAsync(Ct);
        var repo = new FileRepository(ctx);

        (await repo.IsInUseAsync(ThumbId, Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(eventEmbeddedFileId, Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(announcementEmbeddedFileId, Ct)).Should().BeTrue();
        (await repo.IsInUseAsync(Guid.NewGuid(), Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task GetInUseAsync_MixedReferences_ReturnsOnlyReferencedCandidates()
    {
        await using var ctx = postgres.CreateContext();

        var embeddedInEventId = Guid.NewGuid();
        var embeddedInAnnouncementId = Guid.NewGuid();
        var embeddedInResourceId = Guid.NewGuid();
        var embeddedButNotCandidateId = Guid.NewGuid();
        var unreferencedId = Guid.NewGuid();

        var ev = NewEvent();
        ev.Description =
            $"{{\"a\":\"/api/files/{embeddedInEventId}/content\","
            + $"\"b\":\"/api/files/{embeddedButNotCandidateId}/content\"}}";
        var announcement = NewAnnouncement();
        announcement.Description =
            $"{{\"img\":\"https://api.example.org/api/files/{embeddedInAnnouncementId}/content\"}}";
        var resource = NewResource();
        resource.Description = $"{{\"img\":\"/api/files/{embeddedInResourceId}/content\"}}";
        ctx.AddRange(ev, announcement, resource);
        await ctx.SaveChangesAsync(Ct);
        var repo = new FileRepository(ctx);

        var inUse = await repo.GetInUseAsync(
            [
                ThumbId,
                embeddedInEventId,
                embeddedInAnnouncementId,
                embeddedInResourceId,
                unreferencedId,
            ],
            Ct
        );

        inUse
            .Should()
            .BeEquivalentTo([
                ThumbId,
                embeddedInEventId,
                embeddedInAnnouncementId,
                embeddedInResourceId,
            ]);
    }

    [Fact]
    public async Task GetInUseAsync_EmptyInput_ReturnsEmpty()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new FileRepository(ctx);

        var inUse = await repo.GetInUseAsync([], Ct);

        inUse.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignmentExistsAsync_AssignmentPresentOrAbsent_ReportsExistence()
    {
        await using var ctx = postgres.CreateContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var assigned = NewActivity(ev.Id, "Con inscripción");
        var unassigned = NewActivity(ev.Id, "Sin inscripción");
        ctx.AddRange(user, role, status, ev, assigned, unassigned);
        ctx.ActivityUserRoleAssignments.Add(
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = assigned.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(Ct);
        var repo = new ActivityRepository(ctx);

        (await repo.AssignmentExistsAsync(user.Id, assigned.Id, Ct)).Should().BeTrue();
        (await repo.AssignmentExistsAsync(user.Id, unassigned.Id, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_EventCategoryTypeMatchesById_DeletesImmediatelyAndReturnsOne()
    {
        await using var ctx = postgres.CreateContext();
        var categoryType = new EventCategoryType
        {
            Id = Guid.NewGuid(),
            Name = "Efímera",
            Color = "#123456",
        };
        ctx.EventCategoryTypes.Add(categoryType);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventCategoryTypeRepository(ctx);

        var removed = await repo.RemoveAsync(x => x.Id == categoryType.Id, Ct);

        removed.Should().Be(1);
        await using var probe = postgres.CreateContext();
        (await probe.EventCategoryTypes.CountAsync(x => x.Id == categoryType.Id, Ct))
            .Should()
            .Be(0, "ExecuteDelete removes the row without a SaveChanges call");
    }

    [Fact]
    public async Task RemoveAsync_EventCategoryTypeMissing_ReturnsZero()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new EventCategoryTypeRepository(ctx);
        var missingId = Guid.NewGuid();

        (await repo.RemoveAsync(x => x.Id == missingId, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task SetFeaturedAsync_AnotherEventWasFeatured_LeavesExactlyTargetFeatured()
    {
        await using var ctx = postgres.CreateContext();
        var previous = NewEvent("Anterior", featured: true);
        var target = NewEvent("Objetivo");
        ctx.Events.AddRange(previous, target);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventRepository(ctx);

        var result = await repo.SetFeaturedAsync(target.Id, Ct);

        result.Should().BeTrue();
        await using var probe = postgres.CreateContext();
        var featuredIds = await probe
            .Events.Where(e => e.Featured)
            .Select(e => e.Id)
            .ToListAsync(Ct);
        featuredIds.Should().Equal(target.Id);
    }

    [Fact]
    public async Task SetFeaturedAsync_EventMissing_ReturnsFalseWithoutChangingFlags()
    {
        await using var ctx = postgres.CreateContext();
        var featured = NewEvent("Destacado", featured: true);
        ctx.Events.Add(featured);
        await ctx.SaveChangesAsync(Ct);
        var repo = new EventRepository(ctx);

        var result = await repo.SetFeaturedAsync(Guid.NewGuid(), Ct);

        result.Should().BeFalse();
        await using var probe = postgres.CreateContext();
        (await probe.Events.CountAsync(e => e.Featured, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task SetFeaturedAsync_AnotherAnnouncementWasFeatured_LeavesExactlyTargetFeatured()
    {
        await using var ctx = postgres.CreateContext();
        var previous = NewAnnouncement("Anterior", featured: true);
        var target = NewAnnouncement("Objetivo");
        ctx.Announcements.AddRange(previous, target);
        await ctx.SaveChangesAsync(Ct);
        var repo = new AnnouncementRepository(ctx);

        var result = await repo.SetFeaturedAsync(target.Id, Ct);

        result.Should().BeTrue();
        await using var probe = postgres.CreateContext();
        var featuredIds = await probe
            .Announcements.Where(a => a.Featured)
            .Select(a => a.Id)
            .ToListAsync(Ct);
        featuredIds.Should().Equal(target.Id);
    }

    [Fact]
    public async Task SetFeaturedAsync_AnnouncementMissing_ReturnsFalse()
    {
        await using var ctx = postgres.CreateContext();
        var repo = new AnnouncementRepository(ctx);

        (await repo.SetFeaturedAsync(Guid.NewGuid(), Ct)).Should().BeFalse();
    }
}
