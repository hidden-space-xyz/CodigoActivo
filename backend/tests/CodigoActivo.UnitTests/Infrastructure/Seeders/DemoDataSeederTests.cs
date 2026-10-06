using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
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

    private List<Assignment> Assignments =>
        [.. graph.Activities.SelectMany(activity => activity.Assignments)];

    private List<EventTermsDocument> EventTermsDocuments =>
        [.. graph.Events.SelectMany(ev => ev.TermsDocuments)];

    private List<EventCategory> EventCategories =>
        [.. graph.Events.SelectMany(ev => ev.Categories)];

    [Fact]
    public void BuildGraphAdultsGetARandomThrowawayPasswordThatChangesEveryRun()
    {
        var adults = graph.Users.Where(user => user.ParentId is null).ToList();
        var hashes = adults.Select(user => user.PasswordHash).Distinct().ToList();

        adults.Should().NotBeEmpty();
        hashes.Should().ContainSingle("one random password is hashed per seeding run");
        var password = hashes[0]![FakePasswordHasher.Prefix.Length..];
        Convert
            .FromBase64String(password)
            .Should()
            .HaveCount(32, "the password carries 32 bytes of entropy");

        var rerun = DemoDataSeeder.BuildGraph(clock, new FakePasswordHasher());
        rerun
            .Users.First(user => user.ParentId is null)
            .PasswordHash.Should()
            .NotBe(hashes[0], "the password must not be predictable across runs");
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
        Assignments.Should().HaveCount(475, "the last event has not opened its signup yet");
        graph.Ratings.Should().HaveCount(36);
        graph.News.Should().HaveCount(10);
        graph.Resources.Should().HaveCount(20);
        graph.Partners.Should().HaveCount(10);
        graph.CategoryTypes.Should().HaveCount(8);
        graph.TermsDocuments.Should().HaveCount(2);
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
                ev.Calendar.End.Should().BeOnOrAfter(ev.Calendar.Start);
                ev.SignupWindow.EndsAt.Should().BeAfter(ev.SignupWindow.StartsAt);
                LocalDate(ev.SignupWindow.StartsAt).Should().BeOnOrBefore(ev.Calendar.End);
                ev.CreatedAt.Should().BeOnOrBefore(ev.SignupWindow.StartsAt);
                ev.CreatedAt.Should().BeOnOrBefore(clock.UtcNow);
            });
    }

    [Fact]
    public void BuildGraphDefaultLeavesFiveUpcomingEventsAndFinishesTheRest()
    {
        var upcoming = graph.Events.Where(e => e.Calendar.End >= clock.Today).ToList();
        var finished = graph.Events.Where(e => e.Calendar.End < clock.Today).ToList();

        upcoming.Should().HaveCount(5);
        finished.Should().HaveCount(15);
    }

    [Fact]
    public void BuildGraphDefaultFeaturesExactlyOneUpcomingEvent()
    {
        var featured = graph.Events.Where(e => e.Featured).ToList();

        featured.Should().ContainSingle();
        featured[0].Calendar.End.Should().BeOnOrAfter(clock.Today);
    }

    [Fact]
    public void BuildGraphDefaultFeaturesExactlyOneNewsItem()
    {
        graph.News.Should().ContainSingle(a => a.Featured);
    }

    [Fact]
    public void BuildGraphDefaultKeepsSomeUpcomingSignupsOpen()
    {
        var open = graph.Events.Where(e =>
            e.Calendar.End >= clock.Today
            && e.SignupWindow.StartsAt <= clock.UtcNow
            && e.SignupWindow.EndsAt >= clock.UtcNow
        );

        open.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildGraphDefaultSignupTimestampsAreCoherent()
    {
        var eventByActivity = graph.Activities.ToDictionary(a => a.Id, a => a.EventId);
        var eventsById = graph.Events.ToDictionary(e => e.Id);

        Assignments
            .Should()
            .AllSatisfy(assignment =>
            {
                var ev = eventsById[eventByActivity[assignment.ActivityId]];
                assignment.CreatedAt.Should().BeOnOrAfter(ev.CreatedAt);
                assignment.CreatedAt.Should().BeOnOrBefore(ev.SignupWindow.EndsAt);
                assignment.CreatedAt.Should().BeOnOrBefore(clock.UtcNow);
            });
    }

    [Fact]
    public void BuildGraphDefaultEveryEventReferencesSeededTermsDocument()
    {
        var termsDocumentIds = graph.TermsDocuments.Select(t => t.Id).ToHashSet();
        var eventIds = graph.Events.Select(e => e.Id).ToHashSet();
        var referencedIds = EventTermsDocuments.Select(d => d.TermsDocumentId).ToHashSet();

        graph.TermsDocuments.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        EventTermsDocuments
            .Select(d => d.EventId)
            .ToHashSet()
            .Should()
            .BeEquivalentTo(eventIds, "every event links exactly one seeded terms document");
        EventTermsDocuments.Should().OnlyContain(d => d.IsRequired);
        referencedIds.Should().BeEquivalentTo(termsDocumentIds);
    }

    [Fact]
    public void BuildGraphDefaultEachEventReferencesExistingCategory()
    {
        var categoryIds = graph.CategoryTypes.Select(c => c.Id).ToHashSet();
        var linkedEventIds = EventCategories.Select(x => x.EventId).ToHashSet();

        graph.Events.Should().OnlyContain(ev => linkedEventIds.Contains(ev.Id));
        EventCategories.Should().OnlyContain(x => categoryIds.Contains(x.EventCategoryTypeId));
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
                activity.Schedule.EndsAt.Should().BeAfter(activity.Schedule.StartsAt);
                LocalDate(activity.Schedule.StartsAt).Should().BeOnOrAfter(ev.Calendar.Start);
                LocalDate(activity.Schedule.EndsAt).Should().BeOnOrBefore(ev.Calendar.End);
            });
    }

    [Fact]
    public void BuildGraphDefaultEachActivityHasFiveDistinctUsers()
    {
        var openEventIds = graph
            .Events.Where(ev => ev.SignupWindow.StartsAt < clock.UtcNow)
            .Select(ev => ev.Id)
            .ToHashSet();
        var byActivity = Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity
            .Should()
            .HaveCount(graph.Activities.Count(activity => openEventIds.Contains(activity.EventId)));
        byActivity
            .Should()
            .OnlyContain(g => g.Select(x => x.UserId).Distinct().Take(6).Count() == 5);
    }

    [Fact]
    public void BuildGraphDefaultSignupsFallInsideTheOpenSignupWindow()
    {
        var events = graph.Events.ToDictionary(ev => ev.Id);

        graph
            .Activities.Where(activity => activity.Assignments.Count > 0)
            .Should()
            .OnlyContain(activity =>
                events[activity.EventId].SignupWindow.StartsAt < clock.UtcNow
                && activity.Assignments.All(assignment =>
                    assignment.CreatedAt >= events[activity.EventId].SignupWindow.StartsAt
                    && assignment.CreatedAt <= events[activity.EventId].SignupWindow.EndsAt
                    && assignment.CreatedAt <= clock.UtcNow
                )
            );
        graph
            .Activities.Where(activity =>
                events[activity.EventId].SignupWindow.StartsAt >= clock.UtcNow
            )
            .Should()
            .OnlyContain(activity => activity.Assignments.Count == 0);
    }

    [Fact]
    public void BuildGraphDefaultEachActivityHasExactlyOneLeader()
    {
        var byActivity = Assignments.GroupBy(x => x.ActivityId).ToList();

        byActivity
            .Should()
            .OnlyContain(g => g.Where(x => x.Role == ActivityRole.Leader).Take(2).Count() == 1);
    }

    [Fact]
    public void BuildGraphDefaultEveryAssignedRoleComesFromTheFixedCatalog()
    {
        var catalog = new HashSet<ActivityRole>
        {
            ActivityRole.Leader,
            ActivityRole.Volunteer,
            ActivityRole.Participant,
        };

        Assignments.Should().OnlyContain(x => catalog.Contains(x.Role));
    }

    [Fact]
    public void BuildGraphDefaultLeaderAssignmentsBelongToMemberTypeUsers()
    {
        var memberIds = graph
            .Users.Where(u => u.UserType == UserType.Member)
            .Select(u => u.Id)
            .ToHashSet();

        var leaders = Assignments.Where(x => x.Role == ActivityRole.Leader).ToList();

        leaders.Should().NotBeEmpty();
        leaders.Should().OnlyContain(x => memberIds.Contains(x.UserId));
    }

    [Fact]
    public void BuildGraphDefaultAssignmentsHaveUniqueKeysAndKnownUsers()
    {
        var userIds = graph.Users.Select(u => u.Id).ToHashSet();

        Assignments.Select(x => (x.UserId, x.ActivityId, x.Role)).Should().OnlyHaveUniqueItems();
        Assignments.Should().OnlyContain(x => userIds.Contains(x.UserId));
    }

    [Fact]
    public void BuildGraphDefaultRoleCapacitiesAreDeterministicAndFromTheCatalog()
    {
        var catalog = new HashSet<ActivityRole>
        {
            ActivityRole.Leader,
            ActivityRole.Volunteer,
            ActivityRole.Participant,
        };
        var withCapacities = graph.Activities.Where(a => a.RoleCapacities.Count > 0).ToList();

        withCapacities.Should().NotBeEmpty();
        graph.Activities.Should().Contain(a => a.RoleCapacities.Count == 0);
        withCapacities
            .SelectMany(a => a.RoleCapacities)
            .Should()
            .OnlyContain(c => c.DesiredCount >= 1 && catalog.Contains(c.Role));
        withCapacities
            .Should()
            .AllSatisfy(a => a.RoleCapacities.Select(c => c.Role).Should().OnlyHaveUniqueItems());
    }

    [Fact]
    public void BuildGraphDefaultSomeActivitiesExceedTheirDesiredCounts()
    {
        var overSubscribed = graph.Activities.Where(activity =>
            activity.RoleCapacities.Any(capacity =>
                Assignments
                    .Where(x =>
                        x.ActivityId == activity.Id
                        && x.Role == capacity.Role
                        && x.Status != AssignmentStatus.Denied
                    )
                    .Skip(capacity.DesiredCount)
                    .Any()
            )
        );

        overSubscribed.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildGraphDefaultContainsNoAdmins()
    {
        graph.Users.Should().NotContain(u => u.IsAdmin);
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
    public void BuildGraphDefaultAdultsHaveUniqueValidNationalIdsAndNoBirthDate()
    {
        var adults = graph.Users.Where(u => u.ParentId is null).ToList();

        adults.Should().NotBeEmpty();
        adults
            .Should()
            .AllSatisfy(adult =>
            {
                adult.BirthDate.Should().BeNull();
                adult.NationalId!.Value.Should().HaveLength(9);
                SpanishNationalId.IsValid(adult.NationalId!.Value).Should().BeTrue();
            });
        adults.Select(u => u.NationalId).Should().OnlyHaveUniqueItems();
        adults.Should().Contain(u => u.PromotionalConsent);
        adults.Should().Contain(u => !u.PromotionalConsent);
    }

    [Fact]
    public void BuildGraphDefaultAdultsAreVerifiedMembersOrSponsorsThatCanSignIn()
    {
        var adults = graph.Users.Where(u => u.ParentId is null).ToList();

        adults.Should().NotBeEmpty();
        adults
            .Should()
            .AllSatisfy(adult =>
            {
                adult.Status.Should().Be(UserStatus.Active);
                adult.CanSignIn.Should().BeTrue();
                adult.OtpCodeHash.Should().BeNull();
                adult.LastLoginAt.Should().NotBeNull().And.BeOnOrBefore(clock.UtcNow);
            });
        adults
            .Select(u => u.UserType)
            .Distinct()
            .Should()
            .BeEquivalentTo([UserType.Member, UserType.Sponsor]);
    }

    [Fact]
    public void BuildGraphAnotherDayChildrenAreStillMinorsOnThatDay()
    {
        var later = new TestClock(
            new DateTimeOffset(2040, 3, 15, 10, 0, 0, TimeSpan.Zero),
            new DateOnly(2040, 3, 15)
        );

        var children = DemoDataSeeder
            .BuildGraph(later, new FakePasswordHasher())
            .Users.Where(u => u.ParentId is not null)
            .ToList();

        children.Should().NotBeEmpty();
        children
            .Should()
            .AllSatisfy(child =>
            {
                child.BirthDate.Should().NotBeNull();
                child.BirthDate!.Value.IsMinor(later.Today).Should().BeTrue();
                child.BirthDate.Value.AgeOn(later.Today).Should().BeInRange(9, 14);
            });
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
                child.Status.Should().Be(UserStatus.Dependent);
                child.UserType.Should().Be(UserType.Participant);
                child.BirthDate.Should().NotBeNull();
                child.BirthDate!.Value.Year.Should().BeGreaterThan(2008);
                child.NationalId.Should().BeNull();
                child.PromotionalConsent.Should().BeFalse();
                userIds.Should().Contain(child.ParentId!.Value);
            });
    }

    [Fact]
    public void BuildGraphDefaultFileIdsAreUniqueAndUploadedByTheDemoContentAuthor()
    {
        var demoAuthor = graph.Users.Single(u => u.FirstName == "Lucía");

        graph.Files.Select(f => f.Id).Should().OnlyHaveUniqueItems();
        demoAuthor.IsAdmin.Should().BeFalse();
        graph.Files.Should().OnlyContain(f => f.UploadedBy == demoAuthor.Id);
    }

    [Fact]
    public void BuildGraphDefaultEveryThumbnailReferencesSeededFile()
    {
        var fileIds = graph.Files.Select(f => f.Id).ToHashSet();

        graph.Events.Should().OnlyContain(e => fileIds.Contains(e.ThumbnailId));
        graph.Activities.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
        graph.News.Should().OnlyContain(a => fileIds.Contains(a.ThumbnailId));
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
            .Concat(graph.News.Select(a => a.Description))
            .Concat(graph.Resources.Where(r => r.Url is null).Select(r => r.Description))
            .Concat(graph.TermsDocuments.Select(t => t.Description));

        richText
            .Should()
            .AllSatisfy(value =>
            {
                using var doc = JsonDocument.Parse(value.Json);
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
                eventsById[rating.EventId].Calendar.End.Should().BeBefore(clock.Today)
            );
    }

    [Fact]
    public void BuildGraphDefaultSomeFinishedEventsHaveNoRatings()
    {
        var ratedEventIds = graph.Ratings.Select(r => r.EventId).ToHashSet();
        var finished = graph.Events.Where(e => e.Calendar.End < clock.Today).ToList();

        finished.Should().Contain(ev => ratedEventIds.Contains(ev.Id));
        finished.Should().Contain(ev => !ratedEventIds.Contains(ev.Id));
    }

    [Fact]
    public void BuildGraphDefaultRatingsAreUniquelyIdentified()
    {
        graph.Ratings.Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildGraphDefaultRatingScoresAndAnswersAreWithinContract()
    {
        graph
            .Ratings.Should()
            .OnlyContain(r => r.Score >= EventRating.MinScore && r.Score <= EventRating.MaxScore);
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
                r.ResourceType.Should().Be(ResourceType.External);
                r.Description.Json.Should().Be("{}");
                Uri.TryCreate(r.Url, UriKind.Absolute, out var uri).Should().BeTrue();
                uri!.Scheme.Should().Be(Uri.UriSchemeHttps);
            });

        internals.Should().NotBeEmpty();
        internals
            .Should()
            .AllSatisfy(r =>
            {
                r.ResourceType.Should().Be(ResourceType.Internal);
                r.Description.IsEmpty.Should().BeFalse();
            });
    }
}
