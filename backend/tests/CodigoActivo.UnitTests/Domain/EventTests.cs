using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class EventTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly DateOnly EventStart = new(2026, 8, 1);
    private static readonly DateOnly EventEnd = new(2026, 8, 3);
    private static readonly DateTimeOffset EarlyStart = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    public static TheoryData<
        DateOnly?,
        DateOnly?,
        DateTimeOffset?,
        DateTimeOffset?
    > MissingScheduleDates =>
        new()
        {
            { null, EventEnd, SignupStart, SignupEnd },
            { EventStart, null, SignupStart, SignupEnd },
            { EventStart, EventEnd, null, SignupEnd },
            { EventStart, EventEnd, SignupStart, null },
        };

    public static TheoryData<DateTimeOffset, SignupPhase> PhasesWithEarlySignup =>
        new()
        {
            { EarlyStart.AddSeconds(-1), SignupPhase.Closed },
            { EarlyStart, SignupPhase.EarlyOnly },
            { SignupStart.AddSeconds(-1), SignupPhase.EarlyOnly },
            { SignupStart, SignupPhase.Open },
            { SignupEnd, SignupPhase.Open },
            { SignupEnd.AddSeconds(1), SignupPhase.Closed },
        };

    private static EventSchedule Schedule(DateTimeOffset? earlySignupStart = null)
    {
        return EventSchedule
            .Create(EventStart, EventEnd, earlySignupStart, SignupStart, SignupEnd)
            .Value;
    }

    private static EventTermsLinks Terms(params (Guid TermsDocumentId, bool Required)[] links)
    {
        return EventTermsLinks
            .Create([
                .. links.Select(link => new EventTermsLink(link.TermsDocumentId, link.Required)),
            ])
            .Value;
    }

    private static EventCategorySelection Categories(params Guid[] categoryTypeIds)
    {
        return EventCategorySelection.Create(categoryTypeIds).Value;
    }

    private static Event NewEvent(
        Guid[]? categoryTypeIds = null,
        EventTermsLinks? terms = null,
        EventSchedule? schedule = null
    )
    {
        return Event.Create(
            new EventContent("Feria", "Verano", "{}", Guid.NewGuid()),
            schedule ?? Schedule(),
            Categories(categoryTypeIds ?? [Guid.NewGuid()]),
            terms ?? EventTermsLinks.None,
            Guid.NewGuid(),
            Now
        );
    }

    [Theory]
    [MemberData(nameof(MissingScheduleDates))]
    public void ScheduleCreateMissingDateReturnsScheduleRequired(
        DateOnly? eventStart,
        DateOnly? eventEnd,
        DateTimeOffset? signupStart,
        DateTimeOffset? signupEnd
    )
    {
        var schedule = EventSchedule.Create(eventStart, eventEnd, null, signupStart, signupEnd);

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.EventScheduleRequired);
    }

    [Fact]
    public void ScheduleCreateLastDayBeforeFirstDayReturnsInvalidRange()
    {
        var schedule = EventSchedule.Create(
            EventStart,
            EventStart.AddDays(-1),
            null,
            SignupStart,
            SignupEnd
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.EventScheduleInvalidRange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ScheduleCreateSignupClosingNotAfterOpeningReturnsInvalidRange(int closesHoursAfter)
    {
        var schedule = EventSchedule.Create(
            EventStart,
            EventEnd,
            null,
            SignupStart,
            SignupStart.AddHours(closesHoursAfter)
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.EventScheduleInvalidRange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ScheduleCreateEarlySignupNotBeforeSignupReturnsEarlySignupNotBeforeSignup(
        int opensHoursAfterSignup
    )
    {
        var schedule = EventSchedule.Create(
            EventStart,
            EventEnd,
            SignupStart.AddHours(opensHoursAfterSignup),
            SignupStart,
            SignupEnd
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.EventEarlySignupNotBeforeSignup);
    }

    [Fact]
    public void ScheduleCreateSignupOpeningAfterLastDayReturnsInvalidRange()
    {
        var opensAt = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        var schedule = EventSchedule.Create(
            EventStart,
            EventEnd,
            null,
            opensAt,
            opensAt.AddDays(1)
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public void ScheduleCreateSignupOpeningOnLastDaySucceeds()
    {
        var opensAt = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

        var schedule = EventSchedule.Create(
            EventStart,
            EventEnd,
            null,
            opensAt,
            opensAt.AddHours(2)
        );

        schedule.IsSuccess.Should().BeTrue();
        schedule.Value.SignupStartsAt.Should().Be(opensAt);
    }

    [Fact]
    public void ScheduleCreateTimesWithOffsetConvertsThemToUtc()
    {
        var offset = TimeSpan.FromHours(2);

        var schedule = EventSchedule
            .Create(
                EventStart,
                EventEnd,
                new DateTimeOffset(2026, 6, 20, 12, 0, 0, offset),
                new DateTimeOffset(2026, 7, 1, 9, 0, 0, offset),
                new DateTimeOffset(2026, 7, 20, 23, 30, 0, offset)
            )
            .Value;

        schedule.EventStartsAt.Should().Be(EventStart);
        schedule.EventEndsAt.Should().Be(EventEnd);
        schedule
            .EarlySignupStartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero));
        schedule
            .SignupStartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero));
        schedule
            .SignupEndsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 7, 20, 21, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ScheduleCreateWithoutEarlySignupLeavesItMissing()
    {
        Schedule().EarlySignupStartsAt.Should().BeNull();
    }

    [Fact]
    public void TermsLinksCreateDocumentLinkedTwiceReturnsTermsDocumentDuplicated()
    {
        var termsDocumentId = Guid.NewGuid();

        var links = EventTermsLinks.Create([
            new EventTermsLink(termsDocumentId, true),
            new EventTermsLink(termsDocumentId, false),
        ]);

        links.ShouldFail(ErrorKind.Validation, ErrorCode.EventTermsDocumentDuplicated);
    }

    [Fact]
    public void TermsLinksCreateNoLinksReturnsNone()
    {
        EventTermsLinks.Create(null).Value.Should().BeSameAs(EventTermsLinks.None);
        EventTermsLinks.Create([]).Value.Should().BeSameAs(EventTermsLinks.None);
        EventTermsLinks.None.Items.Should().BeEmpty();
    }

    [Fact]
    public void TermsLinksCreateDistinctDocumentsKeepsThemInOrder()
    {
        var first = new EventTermsLink(Guid.NewGuid(), false);
        var second = new EventTermsLink(Guid.NewGuid(), true);

        var links = EventTermsLinks.Create([first, second]);

        links.Value.Items.Should().Equal(first, second);
    }

    [Fact]
    public void CategorySelectionCreateNullReturnsEventCategoriesRequired()
    {
        var selection = EventCategorySelection.Create(null);

        selection.ShouldFail(ErrorKind.Validation, ErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public void CategorySelectionCreateEmptyReturnsEventCategoriesRequired()
    {
        var selection = EventCategorySelection.Create([]);

        selection.ShouldFail(ErrorKind.Validation, ErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public void CategorySelectionCreateRepeatedCategoriesKeepsEachOnceInTheGivenOrder()
    {
        var talleres = Guid.NewGuid();
        var charlas = Guid.NewGuid();
        var concursos = Guid.NewGuid();

        var selection = EventCategorySelection.Create([
            charlas,
            talleres,
            charlas,
            concursos,
            talleres,
        ]);

        selection.IsSuccess.Should().BeTrue();
        selection.Value.CategoryTypeIds.Should().Equal(charlas, talleres, concursos);
    }

    [Fact]
    public void CreateContentWithSpacesStoresTrimmedTitlesScheduleUnfeaturedAndAuthor()
    {
        var authorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        var ev = Event.Create(
            new EventContent("  Feria  ", " Verano ", "{\"a\":1}", thumbnailId),
            Schedule(EarlyStart),
            Categories(Guid.NewGuid()),
            EventTermsLinks.None,
            authorId,
            Now
        );

        ev.Id.Should().NotBeEmpty();
        ev.Title.Should().Be("Feria");
        ev.Subtitle.Should().Be("Verano");
        ev.Description.Should().Be("{\"a\":1}");
        ev.ThumbnailId.Should().Be(thumbnailId);
        ev.EventStartsAt.Should().Be(EventStart);
        ev.EventEndsAt.Should().Be(EventEnd);
        ev.EarlySignupStartsAt.Should().Be(EarlyStart);
        ev.SignupStartsAt.Should().Be(SignupStart);
        ev.SignupEndsAt.Should().Be(SignupEnd);
        ev.Featured.Should().BeFalse();
        ev.TermsDocuments.Should().BeEmpty();
        ev.CreatedBy.Should().Be(authorId);
        ev.CreatedAt.Should().Be(Now);
        ev.UpdatedBy.Should().BeNull();
        ev.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateRepeatedCategoriesLinksEachOnceToTheEvent()
    {
        var talleres = Guid.NewGuid();
        var charlas = Guid.NewGuid();

        var ev = NewEvent(categoryTypeIds: [talleres, charlas, talleres]);

        ev.Categories.Select(category => category.EventCategoryTypeId)
            .Should()
            .BeEquivalentTo([talleres, charlas]);
        ev.Categories.Should().OnlyContain(category => category.EventId == ev.Id);
    }

    [Fact]
    public void CreateTermsLinksDocumentsInDisplayOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var ev = NewEvent(terms: Terms((first, true), (second, false), (third, true)));

        ev.TermsDocuments.OrderBy(document => document.DisplayOrder)
            .Select(document =>
                (document.TermsDocumentId, document.IsRequired, document.DisplayOrder)
            )
            .Should()
            .Equal((first, true, 0), (second, false, 1), (third, true, 2));
        ev.TermsDocuments.Should().OnlyContain(document => document.EventId == ev.Id);
    }

    [Fact]
    public void UpdateNewContentReplacesItAndRecordsEditor()
    {
        var ev = NewEvent();
        var authorId = ev.CreatedBy;
        var editorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var schedule = EventSchedule
            .Create(
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 2),
                null,
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)
            )
            .Value;

        ev.Update(
            new EventContent(" Nueva ", " Otoño  ", "{\"b\":2}", thumbnailId),
            schedule,
            Categories(Guid.NewGuid()),
            EventTermsLinks.None,
            editorId,
            Now.AddDays(1)
        );

        ev.Title.Should().Be("Nueva");
        ev.Subtitle.Should().Be("Otoño");
        ev.Description.Should().Be("{\"b\":2}");
        ev.ThumbnailId.Should().Be(thumbnailId);
        ev.EventStartsAt.Should().Be(new DateOnly(2026, 9, 1));
        ev.EventEndsAt.Should().Be(new DateOnly(2026, 9, 2));
        ev.EarlySignupStartsAt.Should().BeNull();
        ev.SignupStartsAt.Should().Be(schedule.SignupStartsAt);
        ev.SignupEndsAt.Should().Be(schedule.SignupEndsAt);
        ev.CreatedBy.Should().Be(authorId);
        ev.CreatedAt.Should().Be(Now);
        ev.UpdatedBy.Should().Be(editorId);
        ev.UpdatedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void UpdateCategoriesKeepsWantedOnesAddsNewAndDropsTheRest()
    {
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        var added = Guid.NewGuid();
        var ev = NewEvent(categoryTypeIds: [kept, dropped]);
        var keptCategory = ev.Categories.Single(category => category.EventCategoryTypeId == kept);

        ev.Update(
            new EventContent("Feria", "Verano", "{}", ev.ThumbnailId),
            Schedule(),
            Categories(kept, added),
            EventTermsLinks.None,
            Guid.NewGuid(),
            Now
        );

        ev.Categories.Select(category => category.EventCategoryTypeId)
            .Should()
            .BeEquivalentTo([kept, added]);
        ev.Categories.Should().Contain(keptCategory);
        ev.Categories.Should().OnlyContain(category => category.EventId == ev.Id);
    }

    [Fact]
    public void UpdateTermsKeepsLinkedDocumentsPlacesThemAgainAndDropsTheRest()
    {
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        var added = Guid.NewGuid();
        var ev = NewEvent(terms: Terms((kept, true), (dropped, false)));
        var keptDocument = ev.TermsDocuments.Single(document => document.TermsDocumentId == kept);

        ev.Update(
            new EventContent("Feria", "Verano", "{}", ev.ThumbnailId),
            Schedule(),
            Categories(Guid.NewGuid()),
            Terms((added, true), (kept, false)),
            Guid.NewGuid(),
            Now
        );

        ev.TermsDocuments.OrderBy(document => document.DisplayOrder)
            .Select(document =>
                (document.TermsDocumentId, document.IsRequired, document.DisplayOrder)
            )
            .Should()
            .Equal((added, true, 0), (kept, false, 1));
        ev.TermsDocuments.Should().Contain(keptDocument);
    }

    [Theory]
    [MemberData(nameof(PhasesWithEarlySignup))]
    public void SignupPhaseAtWithEarlySignupFollowsTheWindows(
        DateTimeOffset now,
        SignupPhase expected
    )
    {
        var ev = NewEvent(schedule: Schedule(EarlyStart));

        ev.SignupPhaseAt(now).Should().Be(expected);
    }

    [Fact]
    public void SignupPhaseAtWithoutEarlySignupIsClosedBeforeTheSignupOpens()
    {
        var ev = NewEvent();

        ev.SignupPhaseAt(EarlyStart).Should().Be(SignupPhase.Closed);
        ev.SignupPhaseAt(SignupStart.AddSeconds(-1)).Should().Be(SignupPhase.Closed);
        ev.SignupPhaseAt(SignupStart).Should().Be(SignupPhase.Open);
    }

    [Theory]
    [InlineData(2026, 8, 2, false)]
    [InlineData(2026, 8, 3, false)]
    [InlineData(2026, 8, 4, true)]
    public void HasEndedByDayComparedWithLastDayTellsWhetherItIsOver(
        int year,
        int month,
        int day,
        bool expected
    )
    {
        var ev = NewEvent();

        ev.HasEndedBy(new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Fact]
    public void FeatureThenUnfeatureTogglesTheFlagWithoutAnEdit()
    {
        var ev = NewEvent();

        ev.Feature();
        ev.Featured.Should().BeTrue();
        ev.Unfeature();

        ev.Featured.Should().BeFalse();
        ev.UpdatedAt.Should().BeNull();
        ev.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void RatingSubmitBlankAnswersStoresThemAsMissing()
    {
        var eventId = Guid.NewGuid();

        var rating = EventRating.Submit(eventId, 4, "   ", string.Empty, null);

        rating.Id.Should().NotBeEmpty();
        rating.EventId.Should().Be(eventId);
        rating.Score.Should().Be(4);
        rating.MostLiked.Should().BeNull();
        rating.LeastLiked.Should().BeNull();
        rating.Suggestions.Should().BeNull();
    }

    [Fact]
    public void RatingSubmitAnswersWithSpacesStoresThemTrimmed()
    {
        var rating = EventRating.Submit(Guid.NewGuid(), 5, " Bien ", "La cola  ", "  Más talleres");

        rating.MostLiked.Should().Be("Bien");
        rating.LeastLiked.Should().Be("La cola");
        rating.Suggestions.Should().Be("Más talleres");
    }

    [Fact]
    public void TermsAcceptanceRecordStoresTheDecision()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();

        var acceptance = EventTermsAcceptance.Record(eventId, userId, termsDocumentId, false, Now);

        acceptance.EventId.Should().Be(eventId);
        acceptance.UserId.Should().Be(userId);
        acceptance.TermsDocumentId.Should().Be(termsDocumentId);
        acceptance.Accepted.Should().BeFalse();
        acceptance.DecidedAt.Should().Be(Now);
    }

    [Fact]
    public void TermsAcceptanceAcceptDeclinedDocumentAcceptsItAtThatMoment()
    {
        var termsDocumentId = Guid.NewGuid();
        var acceptance = EventTermsAcceptance.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            termsDocumentId,
            false,
            Now
        );

        acceptance.Accept(Now.AddHours(3));

        acceptance.Accepted.Should().BeTrue();
        acceptance.DecidedAt.Should().Be(Now.AddHours(3));
        acceptance.TermsDocumentId.Should().Be(termsDocumentId);
    }
}
