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
    public async Task Query_returns_all_rows_untracked()
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
    public async Task AddAsync_stages_entity_but_does_not_persist_until_save()
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
    public async Task FindAsync_returns_first_match_or_null()
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
    public async Task GetAsync_returns_only_matching_rows()
    {
        await using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("Keep", tier: 5), NewPartner("Drop", tier: 1));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PartnerRepository(ctx);

        var matches = await repo.GetAsync(p => p.Tier == 5, TestContext.Current.CancellationToken);

        matches.Should().ContainSingle(p => p.Name == "Keep");
    }

    [Fact]
    public async Task GetAllAsync_and_CountAsync_and_ExistsAsync_reflect_store()
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
        (await repo.CountAsync(p => p.Tier == 2, TestContext.Current.CancellationToken))
            .Should()
            .Be(2);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(99, false)]
    public async Task ExistsAsync_reports_presence(int tier, bool expected)
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
    public async Task Remove_then_save_deletes_the_entity()
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
    public async Task RemoveAsync_deletes_matching_rows_and_returns_count()
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
    public async Task RemoveAsync_returns_zero_when_no_rows_match()
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
    public async Task GetByIdWithDetailsAsync_includes_status_and_type()
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
    public async Task GetByIdWithDetailsAsync_returns_null_when_missing()
    {
        await using var ctx = NewContext();
        var repo = new UserRepository(ctx);

        (await repo.GetByIdWithDetailsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Theory]
    [InlineData("user@x.test", true)]
    [InlineData("+34600000000", true)]
    [InlineData("nobody@x.test", false)]
    public async Task GetByEmailOrPhoneAsync_matches_either_identifier(
        string identifier,
        bool expectFound
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

        if (expectFound)
        {
            result.Should().NotBeNull();
            result!.Id.Should().Be(user.Id);
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task EmailExistsAsync_honours_exclude_id()
    {
        await using var ctx = NewContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync("dup@x.test", ct: TestContext.Current.CancellationToken))
            .Should()
            .BeTrue();
        (await repo.EmailExistsAsync("free@x.test", ct: TestContext.Current.CancellationToken))
            .Should()
            .BeFalse();
        (
            await repo.EmailExistsAsync(
                "dup@x.test",
                excludeUserId: user.Id,
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse("owner is excluded");
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

    [Fact]
    public async Task PhoneExistsAsync_honours_exclude_id()
    {
        await using var ctx = NewContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync("+100", ct: TestContext.Current.CancellationToken))
            .Should()
            .BeTrue();
        (await repo.PhoneExistsAsync("+999", ct: TestContext.Current.CancellationToken))
            .Should()
            .BeFalse();
        (
            await repo.PhoneExistsAsync(
                "+100",
                excludeUserId: user.Id,
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse();
        (
            await repo.PhoneExistsAsync(
                "+100",
                excludeUserId: Guid.NewGuid(),
                ct: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ListChildrenWithDetailsAsync_returns_children_ordered_with_details()
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
    public async Task GetForEditAsync_includes_categories_and_returns_null_when_missing()
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
    public async Task GetWithActivitiesAndAssignmentsAsync_loads_activity_graph()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(
            new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id }
        );
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
        var loadedActivity = loaded.Activities.Single();
        loadedActivity.Assignments.Should().ContainSingle();
        loadedActivity.AllowedRoleTypes.Should().ContainSingle();
        (
            await repo.GetWithActivitiesAndAssignmentsAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task GetWithAssignmentsAndUsersAsync_loads_nested_user_and_role_graph()
    {
        await using var ctx = NewContext();
        var parent = NewUser("Parent", "P");
        var child = NewUser("Child", "C", parentId: parent.Id);
        var role = NewRoleType("Ponente");
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(parent, child, role, status, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(
            new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id }
        );
        ctx.ActivityUserRoleAssignments.Add(
            new ActivityUserRoleAssignment
            {
                UserId = child.Id,
                ActivityId = activity.Id,
                ActivityRoleTypeId = role.Id,
                AssignmentStatusId = status.Id,
            }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var loaded = await repo.GetWithAssignmentsAndUsersAsync(
            activity.Id,
            TestContext.Current.CancellationToken
        );

        loaded.Should().NotBeNull();
        loaded!.AllowedRoleTypes.Single().ActivityRoleType.Name.Should().Be("Ponente");
        var assignment = loaded.Assignments.Single();
        assignment.User.Parent.Should().NotBeNull();
        assignment.ActivityRoleType.Name.Should().Be("Ponente");
        assignment.AssignmentStatus.Should().NotBeNull();
        (
            await repo.GetWithAssignmentsAndUsersAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task GetForEditAsync_includes_allowed_roles_and_returns_null_when_missing()
    {
        await using var ctx = NewContext();
        var role = NewRoleType();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(role, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(
            new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var loaded = await repo.GetForEditAsync(activity.Id, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.AllowedRoleTypes.Should().ContainSingle();
        (await repo.GetForEditAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Theory]
    [InlineData(10, 50, false)]
    [InlineData(-5, 30, true)]
    [InlineData(10, 60, true)]
    [InlineData(10, 120, true)]
    public async Task AnyOutsideRangeAsync_detects_activities_outside_the_window(
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
    public async Task AnyOutsideRangeAsync_ignores_activities_of_other_events()
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
    public async Task AllowedRoleExistsAsync_reports_presence()
    {
        await using var ctx = NewContext();
        var role = NewRoleType();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(role, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(
            new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id }
        );
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityRepository(ctx);

        (
            await repo.AllowedRoleExistsAsync(
                activity.Id,
                role.Id,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeTrue();
        (
            await repo.AllowedRoleExistsAsync(
                activity.Id,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task GetAssignmentAsync_returns_assignment_with_includes_or_null()
    {
        await using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType("Voluntario");
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
        found!.ActivityRoleType.Name.Should().Be("Voluntario");
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
    public async Task AddAssignmentAsync_stages_without_saving_then_persists_on_save()
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
    public async Task RemoveAssignment_deletes_the_row_on_save()
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
    public async Task GetUserAssignmentsAsync_returns_user_rows_ordered_by_activity_start()
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
    public async Task GetAssignmentsForUsersByEventAsync_filters_by_event_and_user_set()
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
    public async Task QueryAssignments_exposes_all_rows_untracked()
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
    public async Task IsInUseAsync_detects_thumbnail_fks_and_description_embeds()
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
