using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Seeders;

public sealed class DemoDataSeederTests
{
    private readonly TestClock clock = new(
        new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
        new DateOnly(2026, 7, 7)
    );

    private readonly DemoGraph graph;

    public DemoDataSeederTests()
    {
        graph = DemoDataSeeder.BuildGraph(clock, new FakePasswordHasher());
    }

    private DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, clock.TimeZone).DateTime);

    [Fact]
    public void BuildGraph_Default_ProducesExpectedCounts()
    {
        graph.Users.Should().HaveCount(25);
        graph.Events.Should().HaveCount(20);
        graph.Activities.Should().HaveCount(100);
        graph.Assignments.Should().HaveCount(500);
        graph.Announcements.Should().HaveCount(10);
        graph.Resources.Should().HaveCount(20);
        graph.Partners.Should().HaveCount(10);
        graph.CategoryTypes.Should().HaveCount(8);
        graph.Files.Should().HaveCount(180);
    }

    [Fact]
    public void BuildGraph_Default_EachEventHasFiveActivities()
    {
        var activitiesPerEvent = graph.Activities.GroupBy(a => a.EventId).ToList();

        activitiesPerEvent.Should().HaveSameCount(graph.Events);
        activitiesPerEvent.Should().OnlyContain(g => g.Count() == 5);
    }

    [Fact]
    public void BuildGraph_Default_EventSchedulesAreCoherent()
    {
        graph
            .Events.Should()
            .AllSatisfy(ev =>
            {
                ev.EventEndsAt.Should().BeOnOrAfter(ev.EventStartsAt);
                ev.SignupEndsAt.Should().BeAfter(ev.SignupStartsAt);
                LocalDate(ev.SignupStartsAt).Should().BeOnOrBefore(ev.EventEndsAt);
            });
    }

    [Fact]
    public void BuildGraph_Default_EachEventReferencesExistingCategory()
    {
        var categoryIds = graph.CategoryTypes.Select(c => c.Id).ToHashSet();
        var linkedEventIds = graph.EventCategories.Select(x => x.EventId).ToHashSet();

        graph.Events.Should().OnlyContain(ev => linkedEventIds.Contains(ev.Id));
        graph
            .EventCategories.Should()
            .OnlyContain(x => categoryIds.Contains(x.EventCategoryTypeId));
    }

    [Fact]
    public void BuildGraph_Default_ActivitiesFallWithinEventRange()
    {
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        graph
            .Activities.Should()
            .AllSatisfy(activity =>
            {
                var ev = eventsById[activity.EventId];
                activity.ActivityEndsAt.Should().BeAfter(activity.ActivityStartsAt);
                LocalDate(activity.ActivityStartsAt).Should().BeOnOrAfter(ev.EventStartsAt);
                LocalDate(activity.ActivityEndsAt).Should().BeOnOrBefore(ev.EventEndsAt);
            });
    }

    [Fact]
    public void BuildGraph_Default_EachActivityHasFiveDistinctUsers()
    {
        var byActivity = graph.Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity.Should().HaveSameCount(graph.Activities);
        byActivity.Should().OnlyContain(g => g.Select(x => x.UserId).Distinct().Count() == 5);
    }

    [Fact]
    public void BuildGraph_Default_EachActivityHasExactlyOneLeader()
    {
        var byActivity = graph.Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity
            .Should()
            .OnlyContain(g =>
                g.Count(x => x.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader) == 1
            );
    }

    [Fact]
    public void BuildGraph_Default_EveryAssignedRoleIsAllowedOnItsActivity()
    {
        var allowed = graph
            .AllowedRoles.Select(x => (x.ActivityId, x.ActivityRoleTypeId))
            .ToHashSet();

        graph
            .Assignments.Select(x => (x.ActivityId, x.ActivityRoleTypeId))
            .Should()
            .OnlyContain(key => allowed.Contains(key));
    }

    [Fact]
    public void BuildGraph_Default_AssignmentsHaveUniqueKeysAndKnownUsers()
    {
        var userIds = graph.Users.Select(u => u.Id).ToHashSet();

        graph
            .Assignments.Select(x => (x.UserId, x.ActivityId, x.ActivityRoleTypeId))
            .Should()
            .OnlyHaveUniqueItems();
        graph.Assignments.Should().OnlyContain(x => userIds.Contains(x.UserId));
    }

    [Fact]
    public void BuildGraph_Default_ContainsExactlyOneAdmin()
    {
        graph.Users.Should().ContainSingle(u => u.IsAdmin);
    }

    [Fact]
    public void BuildGraph_Default_EmailsAndPhonesAreUnique()
    {
        graph
            .Users.Where(u => u.Email is not null)
            .Select(u => u.Email)
            .Should()
            .OnlyHaveUniqueItems();
        graph
            .Users.Where(u => u.Phone is not null)
            .Select(u => u.Phone)
            .Should()
            .OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildGraph_Default_ChildrenAreDependentParticipantsWithoutCredentials()
    {
        var userIds = graph.Users.Select(u => u.Id).ToHashSet();
        var children = graph.Users.Where(u => u.ParentId is not null).ToList();

        children.Should().NotBeEmpty();
        children
            .Should()
            .AllSatisfy(child =>
            {
                child.Email.Should().BeNull();
                child.Phone.Should().BeNull();
                child.PasswordHash.Should().BeNull();
                child.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
                child.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
                child.BirthDate.Year.Should().BeGreaterThan(2008);
                userIds.Should().Contain(child.ParentId!.Value);
            });
    }

    [Fact]
    public void BuildGraph_Default_FileIdsAreUniqueAndUploadedByTheAdmin()
    {
        var adminId = graph.Users.Single(u => u.IsAdmin).Id;

        graph.Files.Select(f => f.Id).Should().OnlyHaveUniqueItems();
        graph.Files.Should().OnlyContain(f => f.UploadedBy == adminId);
    }

    [Fact]
    public void BuildGraph_Default_EveryThumbnailReferencesSeededFile()
    {
        var fileIds = graph.Files.Select(f => f.Id).ToHashSet();

        graph.Events.Should().OnlyContain(e => fileIds.Contains(e.ThumbnailId));
        graph.Activities.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
        graph.Announcements.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
        graph.Resources.Should().OnlyContain(r => fileIds.Contains(r.ThumbnailId));
        graph.Partners.Should().OnlyContain(p => fileIds.Contains(p.ThumbnailId));
    }

    [Fact]
    public void BuildGraph_Default_EmbeddedEventImagesReferenceSeededFiles()
    {
        var fileIds = graph.Files.Select(f => f.Id).ToHashSet();

        graph
            .Events.Should()
            .AllSatisfy(ev =>
            {
                var referenced = RichTextFileReferences.Extract(ev.Description);
                referenced.Should().NotBeEmpty();
                referenced.Should().OnlyContain(id => fileIds.Contains(id));
            });
    }

    [Fact]
    public void BuildGraph_Default_RichTextDescriptionsAreValidJsonDocuments()
    {
        var richText = graph
            .Events.Select(e => e.Description)
            .Concat(graph.Announcements.Select(a => a.Description))
            .Concat(graph.Resources.Where(r => r.Url is null).Select(r => r.Description));

        richText
            .Should()
            .AllSatisfy(value =>
            {
                using var doc = JsonDocument.Parse(value);
                doc.RootElement.GetProperty("type").GetString().Should().Be("doc");
            });
    }

    [Fact]
    public void BuildGraph_Default_ResourcesMatchTheirTypeContract()
    {
        var external = graph.Resources.Where(r => r.Url is not null).ToList();
        var internals = graph.Resources.Where(r => r.Url is null).ToList();

        external.Should().HaveSameCount(internals);
        external.Should().NotBeEmpty();
        external
            .Should()
            .AllSatisfy(r =>
            {
                r.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.External);
                r.Description.Should().Be("{}");
                Uri.TryCreate(r.Url, UriKind.Absolute, out var uri).Should().BeTrue();
                uri!.Scheme.Should().Be(Uri.UriSchemeHttps);
            });

        internals.Should().NotBeEmpty();
        internals
            .Should()
            .AllSatisfy(r =>
            {
                r.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.Internal);
                RichTextDocument.IsEmpty(r.Description).Should().BeFalse();
            });
    }
}
