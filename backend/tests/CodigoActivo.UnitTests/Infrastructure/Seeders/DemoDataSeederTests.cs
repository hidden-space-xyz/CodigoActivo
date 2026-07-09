using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Seeders;

public sealed class DemoDataSeederTests
{
    private readonly TestClock clock = new(
        new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
        new DateOnly(2026, 7, 7)
    );

    private DemoGraph BuildGraph()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new CodigoActivoDbContext(options);

        var seeder = new DemoDataSeeder(
            context,
            Substitute.For<ILocalFileSystemRepository>(),
            new FakePasswordHasher(),
            clock,
            NullLogger<DemoDataSeeder>.Instance
        );

        return seeder.BuildGraph();
    }

    [Fact]
    public void Builds_the_requested_amount_of_data()
    {
        var graph = BuildGraph();

        graph.Users.Should().HaveCount(25);
        graph.Events.Should().HaveCount(20);
        graph.Activities.Should().HaveCount(100);
        graph.Assignments.Should().HaveCount(500);
        graph.Announcements.Should().HaveCount(10);
        graph.Resources.Should().HaveCount(10);
        graph.Partners.Should().HaveCount(10);
        graph.CategoryTypes.Should().HaveCount(8);
        graph.Files.Should().HaveCount(170);
    }

    [Fact]
    public void Every_event_has_exactly_five_activities()
    {
        var graph = BuildGraph();

        foreach (var ev in graph.Events)
        {
            graph
                .Activities.Count(a => a.EventId == ev.Id)
                .Should()
                .Be(5, "each event must have five activities");
        }
    }

    [Fact]
    public void Event_schedules_are_coherent()
    {
        var graph = BuildGraph();

        foreach (var ev in graph.Events)
        {
            ev.EventEndsAt.Should().BeOnOrAfter(ev.EventStartsAt);
            ev.SignupEndsAt.Should().BeAfter(ev.SignupStartsAt);
            DateOnly
                .FromDateTime(ev.SignupStartsAt.UtcDateTime)
                .Should()
                .BeOnOrBefore(ev.EventEndsAt);
        }
    }

    [Fact]
    public void Every_event_references_at_least_one_existing_category()
    {
        var graph = BuildGraph();
        var categoryIds = graph.CategoryTypes.Select(c => c.Id).ToHashSet();

        foreach (var ev in graph.Events)
        {
            var links = graph.EventCategories.Where(x => x.EventId == ev.Id).ToList();
            links.Should().NotBeEmpty();
            links.Should().OnlyContain(x => categoryIds.Contains(x.EventCategoryTypeId));
        }
    }

    [Fact]
    public void Activities_fall_within_their_event_range()
    {
        var graph = BuildGraph();
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        foreach (var activity in graph.Activities)
        {
            var ev = eventsById[activity.EventId];
            activity.ActivityEndsAt.Should().BeAfter(activity.ActivityStartsAt);

            LocalDate(activity.ActivityStartsAt).Should().BeOnOrAfter(ev.EventStartsAt);
            LocalDate(activity.ActivityEndsAt).Should().BeOnOrBefore(ev.EventEndsAt);
        }
    }

    [Fact]
    public void Each_activity_has_five_distinct_users_with_allowed_roles()
    {
        var graph = BuildGraph();
        var allowedByActivity = graph
            .AllowedRoles.GroupBy(x => x.ActivityId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ActivityRoleTypeId).ToHashSet());

        foreach (var activity in graph.Activities)
        {
            var assignments = graph.Assignments.Where(x => x.ActivityId == activity.Id).ToList();

            assignments.Should().HaveCount(5);
            assignments.Select(x => x.UserId).Distinct().Should().HaveCount(5);
            assignments
                .Should()
                .OnlyContain(x => allowedByActivity[activity.Id].Contains(x.ActivityRoleTypeId));
            assignments
                .Should()
                .ContainSingle(x => x.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader);
        }
    }

    [Fact]
    public void Assignments_have_unique_composite_keys_and_known_users()
    {
        var graph = BuildGraph();
        var userIds = graph.Users.Select(u => u.Id).ToHashSet();

        graph
            .Assignments.Select(x => (x.UserId, x.ActivityId, x.ActivityRoleTypeId))
            .Should()
            .OnlyHaveUniqueItems();
        graph.Assignments.Should().OnlyContain(x => userIds.Contains(x.UserId));
    }

    [Fact]
    public void Users_are_coherent()
    {
        var graph = BuildGraph();

        graph.Users.Count(u => u.IsAdmin).Should().Be(1);

        var withEmail = graph.Users.Where(u => u.Email is not null).Select(u => u.Email).ToList();
        withEmail.Should().OnlyHaveUniqueItems();

        var withPhone = graph.Users.Where(u => u.Phone is not null).Select(u => u.Phone).ToList();
        withPhone.Should().OnlyHaveUniqueItems();

        var userIds = graph.Users.Select(u => u.Id).ToHashSet();
        foreach (var child in graph.Users.Where(u => u.ParentId is not null))
        {
            child.Email.Should().BeNull();
            child.Phone.Should().BeNull();
            child.PasswordHash.Should().BeNull();
            child.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
            child.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
            userIds.Should().Contain(child.ParentId!.Value);
            child.BirthDate.Year.Should().BeGreaterThan(2008);
        }
    }

    [Fact]
    public void All_thumbnails_and_embedded_images_reference_seeded_files()
    {
        var graph = BuildGraph();
        var fileIds = graph.Files.Select(f => f.Id).ToHashSet();

        fileIds.Should().HaveSameCount(graph.Files);

        graph.Events.Select(e => e.ThumbnailId).Should().OnlyContain(id => fileIds.Contains(id));
        graph
            .Activities.Select(a => a.ThumbnailId)
            .Should()
            .OnlyContain(id => fileIds.Contains(id));
        graph
            .Announcements.Select(a => a.ThumbnailId)
            .Should()
            .OnlyContain(id => fileIds.Contains(id));
        graph.Resources.Select(r => r.ThumbnailId).Should().OnlyContain(id => fileIds.Contains(id));
        graph.Partners.Select(p => p.ThumbnailId).Should().OnlyContain(id => fileIds.Contains(id));

        graph.Files.Should().OnlyContain(f => f.UploadedBy == graph.Users[0].Id);

        foreach (var ev in graph.Events)
        {
            var referenced = RichTextFileReferences.Extract(ev.Description);
            referenced.Should().NotBeEmpty();
            referenced.Should().OnlyContain(id => fileIds.Contains(id));
        }
    }

    [Fact]
    public void Rich_text_descriptions_are_valid_documents()
    {
        var graph = BuildGraph();

        var richText = graph
            .Events.Select(e => e.Description)
            .Concat(graph.Announcements.Select(a => a.Description))
            .Concat(graph.Resources.Select(r => r.Description));

        foreach (var value in richText)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            doc.RootElement.GetProperty("type").GetString().Should().Be("doc");
        }
    }

    private DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, clock.TimeZone).DateTime);
}
