using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
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
                .. links.Select(link => new EventTermsLink(
                    TermsDocumentId.From(link.TermsDocumentId),
                    link.Required
                )),
            ])
            .Value;
    }

    private static EventCategorySelection Categories(params EventCategoryTypeId[] categoryTypeIds)
    {
        return EventCategorySelection.Create(categoryTypeIds).Value;
    }

    private static Event NewEvent(
        EventCategoryTypeId[]? categoryTypeIds = null,
        EventTermsLinks? terms = null,
        EventSchedule? schedule = null
    )
    {
        return Event.Create(
            new EventContent(
                "Feria",
                "Verano",
                RichText.From("{}"),
                StoredFileId.From(Guid.NewGuid())
            ),
            schedule ?? Schedule(),
            Categories(categoryTypeIds ?? [EventCategoryTypeId.New()]),
            terms ?? EventTermsLinks.None,
            UserId.From(Guid.NewGuid()),
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

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventScheduleRequired);
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

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventScheduleInvalidRange);
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

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventScheduleInvalidRange);
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

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventEarlySignupNotBeforeSignup);
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

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public void ScheduleCreateSignupClosingAfterLastDayReturnsSignupEndsAfterEvent()
    {
        var schedule = EventSchedule.Create(
            EventStart,
            EventEnd,
            null,
            SignupStart,
            new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero)
        );

        schedule.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventSignupEndsAfterEvent);
    }

    [Fact]
    public void ScheduleCreateSignupClosingOnLastDaySucceeds()
    {
        var closesAt = new DateTimeOffset(2026, 8, 3, 23, 0, 0, TimeSpan.Zero);

        var schedule = EventSchedule.Create(EventStart, EventEnd, null, SignupStart, closesAt);

        schedule.IsSuccess.Should().BeTrue();
        schedule.Value.SignupWindow.EndsAt.Should().Be(closesAt);
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
        schedule.Value.SignupWindow.StartsAt.Should().Be(opensAt);
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

        schedule.Calendar.Start.Should().Be(EventStart);
        schedule.Calendar.End.Should().Be(EventEnd);
        schedule
            .SignupWindow.EarlyStartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero));
        schedule
            .SignupWindow.StartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero));
        schedule
            .SignupWindow.EndsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 7, 20, 21, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ScheduleCreateWithoutEarlySignupLeavesItMissing()
    {
        Schedule().SignupWindow.EarlyStartsAt.Should().BeNull();
    }

    [Fact]
    public void TermsLinksCreateDocumentLinkedTwiceReturnsTermsDocumentDuplicated()
    {
        var termsDocumentId = Guid.NewGuid();

        var links = EventTermsLinks.Create([
            new EventTermsLink(TermsDocumentId.From(termsDocumentId), true),
            new EventTermsLink(TermsDocumentId.From(termsDocumentId), false),
        ]);

        links.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsDocumentDuplicated);
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
        var first = new EventTermsLink(TermsDocumentId.From(Guid.NewGuid()), false);
        var second = new EventTermsLink(TermsDocumentId.From(Guid.NewGuid()), true);

        var links = EventTermsLinks.Create([first, second]);

        links.Value.Items.Should().Equal(first, second);
    }

    [Fact]
    public void CategorySelectionCreateNullReturnsEventCategoriesRequired()
    {
        var selection = EventCategorySelection.Create(null);

        selection.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public void CategorySelectionCreateEmptyReturnsEventCategoriesRequired()
    {
        var selection = EventCategorySelection.Create([]);

        selection.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public void CategorySelectionCreateRepeatedCategoriesKeepsEachOnceInTheGivenOrder()
    {
        var talleres = Guid.NewGuid();
        var charlas = Guid.NewGuid();
        var concursos = Guid.NewGuid();

        var selection = EventCategorySelection.Create([
            EventCategoryTypeId.From(charlas),
            EventCategoryTypeId.From(talleres),
            EventCategoryTypeId.From(charlas),
            EventCategoryTypeId.From(concursos),
            EventCategoryTypeId.From(talleres),
        ]);

        selection.IsSuccess.Should().BeTrue();
        selection
            .Value.CategoryTypeIds.Should()
            .Equal(
                EventCategoryTypeId.From(charlas),
                EventCategoryTypeId.From(talleres),
                EventCategoryTypeId.From(concursos)
            );
    }

    [Fact]
    public void CreateContentWithSpacesStoresTrimmedTitlesScheduleUnfeaturedAndAuthor()
    {
        var authorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        var ev = Event.Create(
            new EventContent(
                "  Feria  ",
                " Verano ",
                RichText.From("{\"a\":1}"),
                StoredFileId.From(thumbnailId)
            ),
            Schedule(EarlyStart),
            Categories(EventCategoryTypeId.From(Guid.NewGuid())),
            EventTermsLinks.None,
            UserId.From(authorId),
            Now
        );

        ev.Id.Value.Should().NotBeEmpty();
        ev.Title.Should().Be("Feria");
        ev.Subtitle.Should().Be("Verano");
        ev.Description!.Json.Should().Be("{\"a\":1}");
        ev.ThumbnailId.Value.Should().Be(thumbnailId);
        ev.Calendar.Start.Should().Be(EventStart);
        ev.Calendar.End.Should().Be(EventEnd);
        ev.SignupWindow.EarlyStartsAt.Should().Be(EarlyStart);
        ev.SignupWindow.StartsAt.Should().Be(SignupStart);
        ev.SignupWindow.EndsAt.Should().Be(SignupEnd);
        ev.Featured.Should().BeFalse();
        ev.TermsDocuments.Should().BeEmpty();
        ev.CreatedBy.Value.Should().Be(authorId);
        ev.CreatedAt.Should().Be(Now);
        ev.UpdatedBy.Should().BeNull();
        ev.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateRepeatedCategoriesLinksEachOnceToTheEvent()
    {
        var talleres = Guid.NewGuid();
        var charlas = Guid.NewGuid();

        var ev = NewEvent(
            categoryTypeIds:
            [
                EventCategoryTypeId.From(talleres),
                EventCategoryTypeId.From(charlas),
                EventCategoryTypeId.From(talleres),
            ]
        );

        ev.Categories.Select(category => category.EventCategoryTypeId.Value)
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
            .Equal(
                (TermsDocumentId.From(first), true, 0),
                (TermsDocumentId.From(second), false, 1),
                (TermsDocumentId.From(third), true, 2)
            );
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
            new EventContent(
                " Nueva ",
                " Otoño  ",
                RichText.From("{\"b\":2}"),
                StoredFileId.From(thumbnailId)
            ),
            schedule,
            Categories(EventCategoryTypeId.From(Guid.NewGuid())),
            EventTermsLinks.None,
            UserId.From(editorId),
            Now.AddDays(1)
        );

        ev.Title.Should().Be("Nueva");
        ev.Subtitle.Should().Be("Otoño");
        ev.Description!.Json.Should().Be("{\"b\":2}");
        ev.ThumbnailId.Value.Should().Be(thumbnailId);
        ev.Calendar.Start.Should().Be(new DateOnly(2026, 9, 1));
        ev.Calendar.End.Should().Be(new DateOnly(2026, 9, 2));
        ev.SignupWindow.EarlyStartsAt.Should().BeNull();
        ev.SignupWindow.StartsAt.Should().Be(schedule.SignupWindow.StartsAt);
        ev.SignupWindow.EndsAt.Should().Be(schedule.SignupWindow.EndsAt);
        ev.CreatedBy.Should().Be(authorId);
        ev.CreatedAt.Should().Be(Now);
        ev.UpdatedBy.Should().Be(UserId.From(editorId));
        ev.UpdatedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void UpdateCategoriesKeepsWantedOnesAddsNewAndDropsTheRest()
    {
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        var added = Guid.NewGuid();
        var ev = NewEvent(
            categoryTypeIds: [EventCategoryTypeId.From(kept), EventCategoryTypeId.From(dropped)]
        );
        var keptCategory = ev.Categories.Single(category =>
            category.EventCategoryTypeId == EventCategoryTypeId.From(kept)
        );

        ev.Update(
            new EventContent("Feria", "Verano", RichText.From("{}"), ev.ThumbnailId),
            Schedule(),
            Categories(EventCategoryTypeId.From(kept), EventCategoryTypeId.From(added)),
            EventTermsLinks.None,
            UserId.From(Guid.NewGuid()),
            Now
        );

        ev.Categories.Select(category => category.EventCategoryTypeId.Value)
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
        var keptDocument = ev.TermsDocuments.Single(document =>
            document.TermsDocumentId == TermsDocumentId.From(kept)
        );

        ev.Update(
            new EventContent("Feria", "Verano", RichText.From("{}"), ev.ThumbnailId),
            Schedule(),
            Categories(EventCategoryTypeId.From(Guid.NewGuid())),
            Terms((added, true), (kept, false)),
            UserId.From(Guid.NewGuid()),
            Now
        );

        ev.TermsDocuments.OrderBy(document => document.DisplayOrder)
            .Select(document =>
                (document.TermsDocumentId, document.IsRequired, document.DisplayOrder)
            )
            .Should()
            .Equal((TermsDocumentId.From(added), true, 0), (TermsDocumentId.From(kept), false, 1));
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

        var rating = EventRating.Submit(EventId.From(eventId), 4, "   ", string.Empty, null).Value;

        rating.Id.Value.Should().NotBeEmpty();
        rating.EventId.Value.Should().Be(eventId);
        rating.Score.Should().Be(4);
        rating.MostLiked.Should().BeNull();
        rating.LeastLiked.Should().BeNull();
        rating.Suggestions.Should().BeNull();
    }

    [Fact]
    public void RatingSubmitAnswersWithSpacesStoresThemTrimmed()
    {
        var rating = EventRating
            .Submit(EventId.From(Guid.NewGuid()), 5, " Bien ", "La cola  ", "  Más talleres")
            .Value;

        rating.MostLiked.Should().Be("Bien");
        rating.LeastLiked.Should().Be("La cola");
        rating.Suggestions.Should().Be("Más talleres");
    }

    [Fact]
    public void RatingSubmitWithoutScoreKeepsTheAnswers()
    {
        var rating = EventRating
            .Submit(EventId.From(Guid.NewGuid()), null, null, null, "Más talleres")
            .Value;

        rating.Score.Should().BeNull();
        rating.Suggestions.Should().Be("Más talleres");
    }

    [Fact]
    public void RatingSubmitWithoutScoreOrAnswersReturnsEmptyError()
    {
        var result = EventRating.Submit(
            EventId.From(Guid.NewGuid()),
            null,
            "  ",
            string.Empty,
            null
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventRatingEmpty);
    }

    [Fact]
    public void TermsAcceptanceRecordStoresTheDecision()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();

        var acceptance = EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(userId),
            TermsDocumentId.From(termsDocumentId),
            false,
            Now
        );

        acceptance.EventId.Value.Should().Be(eventId);
        acceptance.UserId.Value.Should().Be(userId);
        acceptance.TermsDocumentId.Value.Should().Be(termsDocumentId);
        acceptance.Accepted.Should().BeFalse();
        acceptance.DecidedAt.Should().Be(Now);
    }

    [Fact]
    public void TermsAcceptanceAcceptDeclinedDocumentAcceptsItAtThatMoment()
    {
        var termsDocumentId = Guid.NewGuid();
        var acceptance = EventTermsAcceptance.Record(
            EventId.From(Guid.NewGuid()),
            UserId.From(Guid.NewGuid()),
            TermsDocumentId.From(termsDocumentId),
            false,
            Now
        );

        acceptance.Accept(Now.AddHours(3));

        acceptance.Accepted.Should().BeTrue();
        acceptance.DecidedAt.Should().Be(Now.AddHours(3));
        acceptance.TermsDocumentId.Value.Should().Be(termsDocumentId);
    }
}
