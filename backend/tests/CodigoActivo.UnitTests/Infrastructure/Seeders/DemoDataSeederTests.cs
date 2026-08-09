using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
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

    private DateOnly LocalDate(DateTimeOffset value)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, clock.TimeZone).DateTime);
    }

    [Fact]
    public void BuildGraphDefaultProducesExpectedCounts()
    {
        graph.Users.Should().HaveCount(25);
        graph.Events.Should().HaveCount(20);
        graph.Activities.Should().HaveCount(100);
        graph.Assignments.Should().HaveCount(500);
        graph.Ratings.Should().HaveCount(36);
        graph.Announcements.Should().HaveCount(10);
        graph.Resources.Should().HaveCount(20);
        graph.Partners.Should().HaveCount(10);
        graph.CategoryTypes.Should().HaveCount(8);
        graph.Files.Should().HaveCount(180);
    }

    [Fact]
    public void BuildGraphDefaultEachEventHasFiveActivities()
    {
        var activitiesPerEvent = graph.Activities.GroupBy(a => a.EventId).ToList();

        activitiesPerEvent.Should().HaveSameCount(graph.Events);
        activitiesPerEvent.Should().OnlyContain(g => g.Take(6).Count() == 5);
    }

    [Fact]
    public void BuildGraphDefaultEventSchedulesAreCoherent()
    {
        graph
            .Events.Should()
            .AllSatisfy(ev =>
            {
                ev.EventEndsAt.Should().BeOnOrAfter(ev.EventStartsAt);
                ev.SignupEndsAt.Should().BeAfter(ev.SignupStartsAt);
                LocalDate(ev.SignupStartsAt).Should().BeOnOrBefore(ev.EventEndsAt);
                ev.CreatedAt.Should().BeOnOrBefore(ev.SignupStartsAt);
                ev.CreatedAt.Should().BeOnOrBefore(clock.UtcNow);
            });
    }

    [Fact]
    public void BuildGraphDefaultLeavesFiveUpcomingEventsAndFinishesTheRest()
    {
        var upcoming = graph.Events.Where(e => e.EventEndsAt >= clock.Today).ToList();
        var finished = graph.Events.Where(e => e.EventEndsAt < clock.Today).ToList();

        upcoming.Should().HaveCount(5);
        finished.Should().HaveCount(15);
    }

    [Fact]
    public void BuildGraphDefaultFeaturesExactlyOneUpcomingEvent()
    {
        var featured = graph.Events.Where(e => e.Featured).ToList();

        featured.Should().ContainSingle();
        featured[0].EventEndsAt.Should().BeOnOrAfter(clock.Today);
    }

    [Fact]
    public void BuildGraphDefaultFeaturesExactlyOneAnnouncement()
    {
        graph.Announcements.Should().ContainSingle(a => a.Featured);
    }

    [Fact]
    public void BuildGraphDefaultKeepsSomeUpcomingSignupsOpen()
    {
        var open = graph.Events.Where(e =>
            e.EventEndsAt >= clock.Today
            && e.SignupStartsAt <= clock.UtcNow
            && e.SignupEndsAt >= clock.UtcNow
        );

        open.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildGraphDefaultSignupTimestampsAreCoherent()
    {
        var eventByActivity = graph.Activities.ToDictionary(a => a.Id, a => a.EventId);
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        graph
            .Assignments.Should()
            .AllSatisfy(assignment =>
            {
                var ev = eventsById[eventByActivity[assignment.ActivityId]];
                assignment.CreatedAt.Should().BeOnOrAfter(ev.CreatedAt);
                assignment.CreatedAt.Should().BeOnOrBefore(ev.SignupEndsAt);
                assignment.CreatedAt.Should().BeOnOrBefore(clock.UtcNow);
            });
    }

    [Fact]
    public void BuildGraphDefaultEachEventReferencesExistingCategory()
    {
        var categoryIds = graph.CategoryTypes.Select(c => c.Id).ToHashSet();
        var linkedEventIds = graph.EventCategories.Select(x => x.EventId).ToHashSet();

        graph.Events.Should().OnlyContain(ev => linkedEventIds.Contains(ev.Id));
        graph
            .EventCategories.Should()
            .OnlyContain(x => categoryIds.Contains(x.EventCategoryTypeId));
    }

    [Fact]
    public void BuildGraphDefaultActivitiesFallWithinEventRange()
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
    public void BuildGraphDefaultEachActivityHasFiveDistinctUsers()
    {
        var byActivity = graph.Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity.Should().HaveSameCount(graph.Activities);
        byActivity
            .Should()
            .OnlyContain(g => g.Select(x => x.UserId).Distinct().Take(6).Count() == 5);
    }

    [Fact]
    public void BuildGraphDefaultEachActivityHasExactlyOneLeader()
    {
        var byActivity = graph.Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity
            .Should()
            .OnlyContain(g =>
                g.Where(x => x.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader)
                    .Take(2)
                    .Count() == 1
            );
    }

    [Fact]
    public void BuildGraphDefaultEveryAssignedRoleComesFromTheFixedCatalog()
    {
        var catalog = new HashSet<Guid>
        {
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.ActivityRoleTypes.Volunteer,
            SeedIds.ActivityRoleTypes.Participant,
        };

        graph.Assignments.Should().OnlyContain(x => catalog.Contains(x.ActivityRoleTypeId));
    }

    [Fact]
    public void BuildGraphDefaultLeaderAssignmentsBelongToMemberTypeUsers()
    {
        var memberIds = graph
            .Users.Where(u => u.UserTypeId == SeedIds.UserTypes.Member)
            .Select(u => u.Id)
            .ToHashSet();

        var leaders = graph
            .Assignments.Where(x => x.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader)
            .ToList();

        leaders.Should().NotBeEmpty();
        leaders.Should().OnlyContain(x => memberIds.Contains(x.UserId));
    }

    [Fact]
    public void BuildGraphDefaultAssignmentsHaveUniqueKeysAndKnownUsers()
    {
        var userIds = graph.Users.Select(u => u.Id).ToHashSet();

        graph
            .Assignments.Select(x => (x.UserId, x.ActivityId, x.ActivityRoleTypeId))
            .Should()
            .OnlyHaveUniqueItems();
        graph.Assignments.Should().OnlyContain(x => userIds.Contains(x.UserId));
    }

    [Fact]
    public void BuildGraphDefaultRoleCapacitiesAreDeterministicAndFromTheCatalog()
    {
        var catalog = new HashSet<Guid>
        {
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.ActivityRoleTypes.Volunteer,
            SeedIds.ActivityRoleTypes.Participant,
        };
        var withCapacities = graph.Activities.Where(a => a.RoleCapacities.Count > 0).ToList();

        withCapacities.Should().NotBeEmpty();
        graph.Activities.Should().Contain(a => a.RoleCapacities.Count == 0);
        withCapacities
            .SelectMany(a => a.RoleCapacities)
            .Should()
            .OnlyContain(c => c.DesiredCount >= 1 && catalog.Contains(c.ActivityRoleTypeId));
        withCapacities
            .Should()
            .AllSatisfy(a =>
                a.RoleCapacities.Select(c => c.ActivityRoleTypeId).Should().OnlyHaveUniqueItems()
            );
    }

    [Fact]
    public void BuildGraphDefaultSomeActivitiesExceedTheirDesiredCounts()
    {
        var overSubscribed = graph.Activities.Where(activity =>
            activity.RoleCapacities.Any(capacity =>
                graph
                    .Assignments.Where(x =>
                        x.ActivityId == activity.Id
                        && x.ActivityRoleTypeId == capacity.ActivityRoleTypeId
                        && x.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                    )
                    .Skip(capacity.DesiredCount)
                    .Any()
            )
        );

        overSubscribed.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildGraphDefaultContainsExactlyOneAdmin()
    {
        graph.Users.Should().ContainSingle(u => u.IsAdmin);
    }

    [Fact]
    public void BuildGraphDefaultEmailsAndPhonesAreUnique()
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
    public void BuildGraphDefaultChildrenAreDependentParticipantsWithoutCredentials()
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
    public void BuildGraphDefaultFileIdsAreUniqueAndUploadedByTheAdmin()
    {
        var adminId = graph.Users.Single(u => u.IsAdmin).Id;

        graph.Files.Select(f => f.Id).Should().OnlyHaveUniqueItems();
        graph.Files.Should().OnlyContain(f => f.UploadedBy == adminId);
    }

    [Fact]
    public void BuildGraphDefaultEveryThumbnailReferencesSeededFile()
    {
        var fileIds = graph.Files.Select(f => f.Id).ToHashSet();

        graph.Events.Should().OnlyContain(e => fileIds.Contains(e.ThumbnailId));
        graph.Activities.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
        graph.Announcements.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
        graph.Resources.Should().OnlyContain(r => fileIds.Contains(r.ThumbnailId));
        graph.Partners.Should().OnlyContain(p => fileIds.Contains(p.ThumbnailId));
    }

    [Fact]
    public void BuildGraphDefaultEmbeddedEventImagesReferenceSeededFiles()
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
    public void BuildGraphDefaultRichTextDescriptionsAreValidJsonDocuments()
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
    public void BuildGraphDefaultRatingsOnlyTargetFinishedEvents()
    {
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        graph.Ratings.Should().NotBeEmpty();
        graph
            .Ratings.Should()
            .AllSatisfy(rating =>
                eventsById[rating.EventId].EventEndsAt.Should().BeBefore(clock.Today)
            );
    }

    [Fact]
    public void BuildGraphDefaultSomeFinishedEventsHaveNoRatings()
    {
        var ratedEventIds = graph.Ratings.Select(r => r.EventId).ToHashSet();
        var finished = graph.Events.Where(e => e.EventEndsAt < clock.Today).ToList();

        finished.Should().Contain(ev => ratedEventIds.Contains(ev.Id));
        finished.Should().Contain(ev => !ratedEventIds.Contains(ev.Id));
    }

    [Fact]
    public void BuildGraphDefaultRatersHaveAConfirmedAssignmentInTheRatedEvent()
    {
        var eventIdByActivity = graph.Activities.ToDictionary(a => a.Id, a => a.EventId);
        var confirmed = graph
            .Assignments.Where(x =>
                x.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
            )
            .Select(x => (eventIdByActivity[x.ActivityId], x.UserId))
            .ToHashSet();

        graph
            .Ratings.Should()
            .AllSatisfy(rating => confirmed.Should().Contain((rating.EventId, rating.UserId)));
    }

    [Fact]
    public void BuildGraphDefaultRatersCanSignIn()
    {
        var usersById = graph.Users.ToDictionary(u => u.Id);

        graph
            .Ratings.Should()
            .AllSatisfy(rating =>
            {
                var rater = usersById[rating.UserId];
                rater.PasswordHash.Should().NotBeNull();
                rater.ParentId.Should().BeNull();
            });
    }

    [Fact]
    public void BuildGraphDefaultRatingsAreUniquePerEventAndUser()
    {
        graph.Ratings.Select(r => (r.EventId, r.UserId)).Should().OnlyHaveUniqueItems();
        graph.Ratings.Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildGraphDefaultRatingScoresAndAnswersAreWithinContract()
    {
        graph.Ratings.Should().OnlyContain(r => r.Score >= 0 && r.Score <= 5);
        graph
            .Ratings.Should()
            .OnlyContain(r =>
                r.MostLiked == null || r.MostLiked.Length <= EventRating.MaxAnswerLength
            );
        graph
            .Ratings.Should()
            .OnlyContain(r =>
                r.LeastLiked == null || r.LeastLiked.Length <= EventRating.MaxAnswerLength
            );
        graph
            .Ratings.Should()
            .OnlyContain(r =>
                r.Suggestions == null || r.Suggestions.Length <= EventRating.MaxAnswerLength
            );
        graph.Ratings.Should().Contain(r => r.MostLiked == null);
        graph.Ratings.Should().Contain(r => r.MostLiked != null);
    }

    [Fact]
    public void BuildGraphDefaultRatingTimestampsFallBetweenTheEventAndNow()
    {
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        graph
            .Ratings.Should()
            .AllSatisfy(rating =>
            {
                LocalDate(rating.CreatedAt)
                    .Should()
                    .BeOnOrAfter(eventsById[rating.EventId].EventEndsAt);
                rating.CreatedAt.Should().BeOnOrBefore(clock.UtcNow);
                if (rating.UpdatedAt is { } updatedAt)
                {
                    updatedAt.Should().BeOnOrAfter(rating.CreatedAt);
                    updatedAt.Should().BeOnOrBefore(clock.UtcNow);
                }
            });
    }

    [Fact]
    public void BuildGraphDefaultResourcesMatchTheirTypeContract()
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
