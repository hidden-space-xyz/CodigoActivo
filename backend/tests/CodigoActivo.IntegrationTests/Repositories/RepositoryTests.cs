using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CodigoActivo.IntegrationTests.Repositories;

/// <summary>
/// Exercises the repository implementations directly against a real <see cref="CodigoActivoDbContext"/>
/// backed by the EF Core in-memory provider. The generic <c>Repository&lt;T&gt;</c> base is covered
/// through <see cref="PartnerRepository"/>; each concrete repository's custom methods are covered against
/// hand-seeded graphs. Repositories never call <c>SaveChanges</c>, so the tests save explicitly and
/// verify persistence (and the absence of it) through independent contexts sharing the same store.
/// </summary>
public sealed class RepositoryTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CodigoActivoDbContext NewContext(string database) =>
        new(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseInMemoryDatabase(database)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options
        );

    private static CodigoActivoDbContext NewContext() => NewContext(Guid.NewGuid().ToString());

    // A SQLite in-memory database (with FK enforcement off, to match the lax in-memory provider) for
    // the handful of methods that use relational-only EF features such as ExecuteUpdate. The database
    // lives only while the connection is open, so callers keep one connection and build contexts on it.
    private static SqliteConnection OpenSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        connection.Open();
        return connection;
    }

    private static CodigoActivoDbContext NewSqliteContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<CodigoActivoDbContext>().UseSqlite(connection).Options);

    // ---- builders ----------------------------------------------------------

    private static Partner NewPartner(string name = "Partner", int tier = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            FromDate = new DateOnly(2024, 1, 1),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Fixed,
            CreatedBy = Guid.NewGuid(),
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
            UserStatusTypeId = statusId ?? Guid.NewGuid(),
            UserTypeId = userTypeId ?? Guid.NewGuid(),
            ParentId = parentId,
            CreatedAt = Fixed,
        };

    private static UserStatusType NewStatus(string name = "Active") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "d",
            Color = "#fff",
        };

    private static UserType NewUserType(string name = "Member") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "d",
            Color = "#000",
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
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Fixed,
            CreatedBy = Guid.NewGuid(),
        };

    private static Announcement NewAnnouncement(string title = "Ann", bool featured = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "sub",
            Description = "{}",
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Fixed,
            CreatedBy = Guid.NewGuid(),
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
            ActivityModalityTypeId = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = Fixed,
            CreatedBy = Guid.NewGuid(),
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

    // =======================================================================
    //  Generic Repository<T> base — via PartnerRepository
    // =======================================================================

    [Fact]
    public async Task Query_returns_all_rows_untracked()
    {
        using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("A"), NewPartner("B"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new PartnerRepository(ctx);

        var items = await repo.Query().ToListAsync();

        items.Should().HaveCount(2);
        ctx.ChangeTracker.Entries<Partner>().Should().BeEmpty("Query() uses AsNoTracking");
    }

    [Fact]
    public async Task AddAsync_stages_entity_but_does_not_persist_until_save()
    {
        var database = Guid.NewGuid().ToString();
        var partner = NewPartner();
        await using (var ctx = NewContext(database))
        {
            var repo = new PartnerRepository(ctx);
            await repo.AddAsync(partner);

            await using var probe = NewContext(database);
            (await probe.Partners.CountAsync()).Should().Be(0, "the repository must not call SaveChanges");

            await ctx.SaveChangesAsync();
        }

        await using var verify = NewContext(database);
        (await verify.Partners.FindAsync(partner.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task FindAsync_returns_first_match_or_null()
    {
        using var ctx = NewContext();
        var target = NewPartner("Target");
        ctx.Partners.AddRange(target, NewPartner("Other"));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        var found = await repo.FindAsync(p => p.Name == "Target");
        found.Should().NotBeNull();
        found!.Id.Should().Be(target.Id);
        (await repo.FindAsync(p => p.Name == "Missing")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_returns_only_matching_rows()
    {
        using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("Keep", tier: 5), NewPartner("Drop", tier: 1));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        var matches = await repo.GetAsync(p => p.Tier == 5);

        matches.Should().ContainSingle(p => p.Name == "Keep");
    }

    [Fact]
    public async Task GetAllAsync_and_CountAsync_and_ExistsAsync_reflect_store()
    {
        using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("A", tier: 2), NewPartner("B", tier: 2), NewPartner("C", tier: 9));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        (await repo.GetAllAsync()).Should().HaveCount(3);
        (await repo.CountAsync(p => p.Tier == 2)).Should().Be(2);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(99, false)]
    public async Task ExistsAsync_reports_presence(int tier, bool expected)
    {
        using var ctx = NewContext();
        ctx.Partners.Add(NewPartner("A", tier: 9));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        (await repo.ExistsAsync(p => p.Tier == tier)).Should().Be(expected);
    }

    [Fact]
    public async Task Remove_then_save_deletes_the_entity()
    {
        using var ctx = NewContext();
        var partner = NewPartner();
        ctx.Partners.Add(partner);
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        repo.Remove(partner);
        await ctx.SaveChangesAsync();

        (await ctx.Partners.FindAsync(partner.Id)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_deletes_matching_rows_and_returns_count()
    {
        using var ctx = NewContext();
        ctx.Partners.AddRange(NewPartner("X", tier: 1), NewPartner("Y", tier: 1), NewPartner("Z", tier: 8));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        var removed = await repo.RemoveAsync(p => p.Tier == 1);
        await ctx.SaveChangesAsync();

        removed.Should().Be(2);
        (await ctx.Partners.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_returns_zero_when_no_rows_match()
    {
        using var ctx = NewContext();
        ctx.Partners.Add(NewPartner("Only", tier: 3));
        await ctx.SaveChangesAsync();
        var repo = new PartnerRepository(ctx);

        (await repo.RemoveAsync(p => p.Tier == 100)).Should().Be(0);
        (await ctx.Partners.CountAsync()).Should().Be(1);
    }

    // =======================================================================
    //  UserRepository
    // =======================================================================

    [Fact]
    public async Task GetByIdWithDetailsAsync_includes_status_and_type()
    {
        using var ctx = NewContext();
        var status = NewStatus("Active");
        var type = NewUserType("Socio");
        var user = NewUser("Ada", "Admin", statusId: status.Id, userTypeId: type.Id);
        ctx.AddRange(status, type, user);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var result = await repo.GetByIdWithDetailsAsync(user.Id);

        result.Should().NotBeNull();
        result!.UserStatusType.Name.Should().Be("Active");
        result.UserType.Name.Should().Be("Socio");
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_returns_null_when_missing()
    {
        using var ctx = NewContext();
        var repo = new UserRepository(ctx);

        (await repo.GetByIdWithDetailsAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Theory]
    [InlineData("user@x.test", true)]
    [InlineData("+34600000000", true)]
    [InlineData("nobody@x.test", false)]
    public async Task GetByEmailOrPhoneAsync_matches_either_identifier(string identifier, bool expectFound)
    {
        using var ctx = NewContext();
        var status = NewStatus();
        var type = NewUserType();
        var user = NewUser("Match", "Me", email: "user@x.test", phone: "+34600000000", statusId: status.Id, userTypeId: type.Id);
        ctx.AddRange(status, type, user);
        await ctx.SaveChangesAsync();
        var repo = new UserRepository(ctx);

        var result = await repo.GetByEmailOrPhoneAsync(identifier);

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
        using var ctx = NewContext();
        var user = NewUser(email: "dup@x.test");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var repo = new UserRepository(ctx);

        (await repo.EmailExistsAsync("dup@x.test")).Should().BeTrue();
        (await repo.EmailExistsAsync("free@x.test")).Should().BeFalse();
        (await repo.EmailExistsAsync("dup@x.test", excludeUserId: user.Id)).Should().BeFalse("owner is excluded");
        (await repo.EmailExistsAsync("dup@x.test", excludeUserId: Guid.NewGuid())).Should().BeTrue("another user still collides");
    }

    [Fact]
    public async Task PhoneExistsAsync_honours_exclude_id()
    {
        using var ctx = NewContext();
        var user = NewUser(phone: "+100");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var repo = new UserRepository(ctx);

        (await repo.PhoneExistsAsync("+100")).Should().BeTrue();
        (await repo.PhoneExistsAsync("+999")).Should().BeFalse();
        (await repo.PhoneExistsAsync("+100", excludeUserId: user.Id)).Should().BeFalse();
        (await repo.PhoneExistsAsync("+100", excludeUserId: Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task ListChildrenWithDetailsAsync_returns_children_ordered_with_details()
    {
        using var ctx = NewContext();
        var status = NewStatus("Dependent");
        var type = NewUserType();
        var parent = NewUser("Parent", "P", statusId: status.Id, userTypeId: type.Id);
        var zoe = NewUser("Zoe", "Child", statusId: status.Id, parentId: parent.Id, userTypeId: type.Id);
        var amy = NewUser("Amy", "Child", statusId: status.Id, parentId: parent.Id, userTypeId: type.Id);
        var stranger = NewUser("Stranger", "S", statusId: status.Id, userTypeId: type.Id);
        ctx.AddRange(status, type, parent, zoe, amy, stranger);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new UserRepository(ctx);

        var children = await repo.ListChildrenWithDetailsAsync(parent.Id);

        children.Select(c => c.FirstName).Should().Equal("Amy", "Zoe");
        children.First().UserStatusType.Name.Should().Be("Dependent");
    }

    [Fact]
    public async Task ListChildrenWithDetailsAsync_returns_empty_when_no_children()
    {
        using var ctx = NewContext();
        var repo = new UserRepository(ctx);

        (await repo.ListChildrenWithDetailsAsync(Guid.NewGuid())).Should().BeEmpty();
    }

    // =======================================================================
    //  EventRepository
    // =======================================================================

    // SQLite-backed: SetExclusiveFeaturedAsync uses EF ExecuteUpdate, which the in-memory provider
    // cannot run. SQLite is a real relational engine, so it exercises the two bulk updates for real.
    [Fact]
    public async Task SetFeaturedAsync_makes_target_the_only_featured_event()
    {
        await using var connection = OpenSqlite();
        var current = NewEvent("Old", featured: true);
        var next = NewEvent("New", featured: false);
        await using (var ctx = NewSqliteContext(connection))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Events.AddRange(current, next);
            await ctx.SaveChangesAsync();
            var repo = new EventRepository(ctx);

            (await repo.SetFeaturedAsync(next.Id)).Should().BeTrue();
        }

        await using var verify = NewSqliteContext(connection);
        var stored = await verify.Events.AsNoTracking().ToListAsync();
        stored.Single(e => e.Id == next.Id).Featured.Should().BeTrue();
        stored.Single(e => e.Id == current.Id).Featured.Should().BeFalse();
    }

    [Fact]
    public async Task SetFeaturedAsync_returns_false_and_changes_nothing_for_missing_id()
    {
        await using var connection = OpenSqlite();
        var existing = NewEvent("Keep", featured: true);
        await using (var ctx = NewSqliteContext(connection))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Events.Add(existing);
            await ctx.SaveChangesAsync();
            var repo = new EventRepository(ctx);

            (await repo.SetFeaturedAsync(Guid.NewGuid())).Should().BeFalse();
        }

        await using var verify = NewSqliteContext(connection);
        verify.Events.AsNoTracking().Single(e => e.Id == existing.Id).Featured.Should().BeTrue();
    }

    [Fact]
    public async Task GetForEditAsync_includes_categories_and_returns_null_when_missing()
    {
        using var ctx = NewContext();
        var category = new EventCategoryType { Id = Guid.NewGuid(), Name = "Cat", Color = "#111" };
        var ev = NewEvent();
        ctx.AddRange(category, ev);
        ctx.EventCategories.Add(new EventCategory { EventId = ev.Id, EventCategoryTypeId = category.Id });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetForEditAsync(ev.Id);

        loaded.Should().NotBeNull();
        loaded!.Categories.Should().ContainSingle();
        (await repo.GetForEditAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetWithActivitiesAndAssignmentsAsync_loads_activity_graph()
    {
        using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id });
        ctx.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new EventRepository(ctx);

        var loaded = await repo.GetWithActivitiesAndAssignmentsAsync(ev.Id);

        loaded.Should().NotBeNull();
        loaded!.Activities.Should().ContainSingle();
        var loadedActivity = loaded.Activities.Single();
        loadedActivity.Assignments.Should().ContainSingle();
        loadedActivity.AllowedRoleTypes.Should().ContainSingle();
        (await repo.GetWithActivitiesAndAssignmentsAsync(Guid.NewGuid())).Should().BeNull();
    }

    // =======================================================================
    //  ActivityRepository
    // =======================================================================

    [Fact]
    public async Task GetWithAssignmentsAndUsersAsync_loads_nested_user_and_role_graph()
    {
        using var ctx = NewContext();
        var parent = NewUser("Parent", "P");
        var child = NewUser("Child", "C", parentId: parent.Id);
        var role = NewRoleType("Ponente");
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(parent, child, role, status, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id });
        ctx.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
        {
            UserId = child.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var loaded = await repo.GetWithAssignmentsAndUsersAsync(activity.Id);

        loaded.Should().NotBeNull();
        loaded!.AllowedRoleTypes.Single().ActivityRoleType.Name.Should().Be("Ponente");
        var assignment = loaded.Assignments.Single();
        assignment.User.Parent.Should().NotBeNull();
        assignment.ActivityRoleType.Name.Should().Be("Ponente");
        assignment.AssignmentStatus.Should().NotBeNull();
        (await repo.GetWithAssignmentsAndUsersAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetForEditAsync_includes_allowed_roles_and_returns_null_when_missing()
    {
        using var ctx = NewContext();
        var role = NewRoleType();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(role, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var loaded = await repo.GetForEditAsync(activity.Id);

        loaded.Should().NotBeNull();
        loaded!.AllowedRoleTypes.Should().ContainSingle();
        (await repo.GetForEditAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Theory]
    [InlineData(10, 50, false)] // fully inside [0, 60)
    [InlineData(-5, 30, true)] // starts before the lower bound
    [InlineData(10, 60, true)] // ends exactly at the exclusive upper bound
    [InlineData(10, 120, true)] // ends after the upper bound
    public async Task AnyOutsideRangeAsync_detects_activities_outside_the_window(
        int startOffsetMinutes,
        int endOffsetMinutes,
        bool expected
    )
    {
        using var ctx = NewContext();
        var ev = NewEvent();
        var lower = Fixed;
        var upper = Fixed.AddMinutes(60);
        ctx.Events.Add(ev);
        ctx.Activities.Add(
            NewActivity(ev.Id, startsAt: Fixed.AddMinutes(startOffsetMinutes), endsAt: Fixed.AddMinutes(endOffsetMinutes))
        );
        await ctx.SaveChangesAsync();
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(ev.Id, lower, upper)).Should().Be(expected);
    }

    [Fact]
    public async Task AnyOutsideRangeAsync_ignores_activities_of_other_events()
    {
        using var ctx = NewContext();
        var target = NewEvent("Target");
        var other = NewEvent("Other");
        ctx.Events.AddRange(target, other);
        // Only the *other* event has an out-of-range activity.
        ctx.Activities.Add(NewActivity(other.Id, startsAt: Fixed.AddMinutes(-120), endsAt: Fixed));
        ctx.Activities.Add(NewActivity(target.Id, startsAt: Fixed.AddMinutes(10), endsAt: Fixed.AddMinutes(50)));
        await ctx.SaveChangesAsync();
        var repo = new ActivityRepository(ctx);

        (await repo.AnyOutsideRangeAsync(target.Id, Fixed, Fixed.AddMinutes(60))).Should().BeFalse();
    }

    [Fact]
    public async Task AllowedRoleExistsAsync_reports_presence()
    {
        using var ctx = NewContext();
        var role = NewRoleType();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(role, ev, activity);
        ctx.ActivityAllowedRoleTypes.Add(new ActivityAllowedRoleType { ActivityId = activity.Id, ActivityRoleTypeId = role.Id });
        await ctx.SaveChangesAsync();
        var repo = new ActivityRepository(ctx);

        (await repo.AllowedRoleExistsAsync(activity.Id, role.Id)).Should().BeTrue();
        (await repo.AllowedRoleExistsAsync(activity.Id, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task GetAssignmentAsync_returns_assignment_with_includes_or_null()
    {
        using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType("Voluntario");
        var status = NewAssignmentStatus("Pending");
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var found = await repo.GetAssignmentAsync(user.Id, activity.Id);

        found.Should().NotBeNull();
        found!.ActivityRoleType.Name.Should().Be("Voluntario");
        found.AssignmentStatus.Name.Should().Be("Pending");
        (await repo.GetAssignmentAsync(user.Id, Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task AddAssignmentAsync_stages_without_saving_then_persists_on_save()
    {
        var database = Guid.NewGuid().ToString();
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

        await using (var ctx = NewContext(database))
        {
            ctx.AddRange(user, role, status, ev, activity);
            await ctx.SaveChangesAsync();
            var repo = new ActivityRepository(ctx);

            await repo.AddAssignmentAsync(assignment);
            await using (var probe = NewContext(database))
            {
                (await probe.ActivityUserRoleAssignments.CountAsync()).Should().Be(0);
            }

            await ctx.SaveChangesAsync();
        }

        await using var verify = NewContext(database);
        (await verify.ActivityUserRoleAssignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAssignment_deletes_the_row_on_save()
    {
        using var ctx = NewContext();
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
        await ctx.SaveChangesAsync();
        var repo = new ActivityRepository(ctx);

        repo.RemoveAssignment(assignment);
        await ctx.SaveChangesAsync();

        (await ctx.ActivityUserRoleAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetUserAssignmentsAsync_returns_user_rows_ordered_by_activity_start()
    {
        using var ctx = NewContext();
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
            new ActivityUserRoleAssignment { UserId = user.Id, ActivityId = late.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id },
            new ActivityUserRoleAssignment { UserId = user.Id, ActivityId = early.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id },
            new ActivityUserRoleAssignment { UserId = other.Id, ActivityId = foreign.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id }
        );
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var assignments = await repo.GetUserAssignmentsAsync(user.Id);

        assignments.Should().HaveCount(2);
        assignments.Select(a => a.Activity.Title).Should().Equal("Early", "Late");
    }

    [Fact]
    public async Task GetAssignmentsForUsersByEventAsync_filters_by_event_and_user_set()
    {
        using var ctx = NewContext();
        var wanted = NewUser();
        var alsoWanted = NewUser();
        var excludedUser = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var targetEvent = NewEvent("Target");
        var otherEvent = NewEvent("Other");
        var targetActivity = NewActivity(targetEvent.Id);
        var otherActivity = NewActivity(otherEvent.Id);
        ctx.AddRange(wanted, alsoWanted, excludedUser, role, status, targetEvent, otherEvent, targetActivity, otherActivity);
        ctx.ActivityUserRoleAssignments.AddRange(
            new ActivityUserRoleAssignment { UserId = wanted.Id, ActivityId = targetActivity.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id },
            new ActivityUserRoleAssignment { UserId = excludedUser.Id, ActivityId = targetActivity.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id },
            new ActivityUserRoleAssignment { UserId = alsoWanted.Id, ActivityId = otherActivity.Id, ActivityRoleTypeId = role.Id, AssignmentStatusId = status.Id }
        );
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var result = await repo.GetAssignmentsForUsersByEventAsync(
            new[] { wanted.Id, alsoWanted.Id },
            targetEvent.Id
        );

        result.Should().ContainSingle();
        result.Single().UserId.Should().Be(wanted.Id);
        result.Single().ActivityRoleType.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryAssignments_exposes_all_rows_untracked()
    {
        using var ctx = NewContext();
        var user = NewUser();
        var role = NewRoleType();
        var status = NewAssignmentStatus();
        var ev = NewEvent();
        var activity = NewActivity(ev.Id);
        ctx.AddRange(user, role, status, ev, activity);
        ctx.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
        {
            UserId = user.Id,
            ActivityId = activity.Id,
            ActivityRoleTypeId = role.Id,
            AssignmentStatusId = status.Id,
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var repo = new ActivityRepository(ctx);

        var count = await repo.QueryAssignments().CountAsync();

        count.Should().Be(1);
        ctx.ChangeTracker.Entries<ActivityUserRoleAssignment>().Should().BeEmpty();
    }

    // =======================================================================
    //  AnnouncementRepository
    // =======================================================================

    // SQLite-backed (see the Event equivalents): ExecuteUpdate needs a real relational provider.
    [Fact]
    public async Task SetFeaturedAsync_makes_target_the_only_featured_announcement()
    {
        await using var connection = OpenSqlite();
        var current = NewAnnouncement("Old", featured: true);
        var next = NewAnnouncement("New", featured: false);
        await using (var ctx = NewSqliteContext(connection))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Announcements.AddRange(current, next);
            await ctx.SaveChangesAsync();
            var repo = new AnnouncementRepository(ctx);

            (await repo.SetFeaturedAsync(next.Id)).Should().BeTrue();
        }

        await using var verify = NewSqliteContext(connection);
        var stored = await verify.Announcements.AsNoTracking().ToListAsync();
        stored.Single(a => a.Id == next.Id).Featured.Should().BeTrue();
        stored.Single(a => a.Id == current.Id).Featured.Should().BeFalse();
    }

    [Fact]
    public async Task SetFeaturedAsync_returns_false_for_missing_announcement()
    {
        await using var connection = OpenSqlite();
        var existing = NewAnnouncement("Keep", featured: true);
        await using (var ctx = NewSqliteContext(connection))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Announcements.Add(existing);
            await ctx.SaveChangesAsync();
            var repo = new AnnouncementRepository(ctx);

            (await repo.SetFeaturedAsync(Guid.NewGuid())).Should().BeFalse();
        }

        await using var verify = NewSqliteContext(connection);
        verify.Announcements.AsNoTracking().Single(a => a.Id == existing.Id).Featured.Should().BeTrue();
    }
}
