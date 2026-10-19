using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventTermsStateQueryHandlerTests
{
    private static readonly DateTimeOffset DecidedAt = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid eventId = Guid.NewGuid();
    private readonly GetEventTermsStateQueryHandler sut;

    public GetEventTermsStateQueryHandlerTests()
    {
        store.Events.Add(new EventRow { Id = eventId });
        store.Users.Add(Person(userId, "Marta"));
        sut = new GetEventTermsStateQueryHandler(
            store,
            new FakeQueryExecutor(),
            new TestCurrentUser(UserId.From(userId)),
            clock
        );
    }

    private static UserRow Person(
        Guid id,
        string firstName,
        Guid? parentId = null,
        DateOnly? birthDate = null
    )
    {
        return new UserRow
        {
            Id = id,
            FirstName = firstName,
            LastName = "Ruiz",
            ParentId = parentId,
            BirthDate = birthDate,
        };
    }

    private static EventTermsDocumentRow NewDocument(
        Guid eventId,
        Guid termsDocumentId,
        bool required,
        int displayOrder,
        string name = "Documento",
        bool requiresSignature = false,
        TermsDocumentKind kind = TermsDocumentKind.Conditions,
        int version = 1
    )
    {
        return new EventTermsDocumentRow
        {
            EventId = eventId,
            TermsDocumentId = termsDocumentId,
            IsRequired = required,
            DisplayOrder = displayOrder,
            TermsDocument = new TermsDocumentRow
            {
                Id = termsDocumentId,
                Name = name,
                Description = "{}",
                RequiresSignature = requiresSignature,
                Kind = kind,
                Version = version,
            },
        };
    }

    private EventTermsAcceptanceRow Decision(
        Guid personId,
        Guid termsDocumentId,
        bool accepted,
        Guid? eventId = null
    )
    {
        return new EventTermsAcceptanceRow
        {
            EventId = eventId ?? this.eventId,
            UserId = personId,
            TermsDocumentId = termsDocumentId,
            Accepted = accepted,
            DecidedAt = DecidedAt,
        };
    }

    private void HasDocuments(params EventTermsDocumentRow[] documents)
    {
        store.EventTermsDocuments.AddRange(documents);
    }

    private async Task<EventTermsStateResponse> StateAsync()
    {
        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(EventId.From(eventId)),
            TestContext.Current.CancellationToken
        );
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var unknownEventId = Guid.NewGuid();
        HasDocuments(NewDocument(unknownEventId, Guid.NewGuid(), required: true, displayOrder: 0));

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(EventId.From(unknownEventId)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventNotFound);
        store.ReadsOf<EventTermsDocumentRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncEventWithoutDocumentsReturnsNothingToDecide()
    {
        HasDocuments();

        var result = await StateAsync();

        result.Documents.Should().BeEmpty();
        result.People.Should().BeEmpty();
        store.ReadsOf<EventTermsAcceptanceRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncConsentIsNeverRequired()
    {
        var conditionsId = Guid.NewGuid();
        var consentId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, conditionsId, required: true, displayOrder: 0),
            NewDocument(
                eventId,
                consentId,
                required: true,
                displayOrder: 1,
                kind: TermsDocumentKind.Consent
            )
        );

        var result = await StateAsync();

        result
            .Documents.Select(d => (d.TermsDocumentId, d.Kind, d.Required))
            .Should()
            .Equal(
                (conditionsId, TermsDocumentKind.Conditions, true),
                (consentId, TermsDocumentKind.Consent, false)
            );
    }

    [Fact]
    public async Task HandleAsyncListsTheUserFirstAndThenTheirDependentsWithWhoDecidesTheirConsents()
    {
        var teenagerId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        store.Users.AddRange(
            Person(teenagerId, "Bea", userId, clock.Today.AddYears(-14)),
            Person(childId, "Alba", userId, clock.Today.AddYears(-14).AddDays(1)),
            Person(Guid.NewGuid(), "Ajena", Guid.NewGuid(), clock.Today.AddYears(-10))
        );
        HasDocuments(NewDocument(eventId, Guid.NewGuid(), required: true, displayOrder: 0));

        var result = await StateAsync();

        result
            .People.Select(person => (person.UserId, person.FirstName, person.DecidesOwnConsents))
            .Should()
            .Equal((userId, "Marta", true), (childId, "Alba", false), (teenagerId, "Bea", true));
    }

    [Fact]
    public async Task HandleAsyncReturnsTheDecisionsTakenForEachPersonAboutThisEvent()
    {
        var childId = Guid.NewGuid();
        var conditionsId = Guid.NewGuid();
        var consentId = Guid.NewGuid();
        store.Users.Add(Person(childId, "Leo", userId, clock.Today.AddYears(-9)));
        HasDocuments(
            NewDocument(eventId, conditionsId, required: true, displayOrder: 0),
            NewDocument(
                eventId,
                consentId,
                required: false,
                displayOrder: 1,
                kind: TermsDocumentKind.Consent
            )
        );
        store.EventTermsAcceptances.AddRange(
            Decision(userId, conditionsId, true),
            Decision(childId, conditionsId, true),
            Decision(childId, consentId, false),
            Decision(userId, consentId, true, Guid.NewGuid()),
            Decision(Guid.NewGuid(), consentId, true)
        );

        var result = await StateAsync();

        var self = result.People.Should().ContainSingle(person => person.UserId == userId).Subject;
        self.Decisions.Should()
            .Equal(new EventTermsPersonDecisionResponse(conditionsId, true, DecidedAt));
        var child = result
            .People.Should()
            .ContainSingle(person => person.UserId == childId)
            .Subject;
        child
            .Decisions.Select(decision => (decision.TermsDocumentId, decision.Accepted))
            .Should()
            .BeEquivalentTo([(conditionsId, true), (consentId, false)]);
    }

    [Fact]
    public async Task HandleAsyncDocumentsOrderedByDisplayOrderRegardlessOfInputOrder()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, thirdId, required: false, displayOrder: 2, name: "Tercero"),
            NewDocument(eventId, firstId, required: false, displayOrder: 0, name: "Primero"),
            NewDocument(eventId, secondId, required: false, displayOrder: 1, name: "Segundo")
        );

        var result = await StateAsync();

        result.Documents.Select(d => d.TermsDocumentId).Should().Equal(firstId, secondId, thirdId);
    }

    [Fact]
    public async Task HandleAsyncDocumentThatAsksForSignatureTellsSo()
    {
        var signedId = Guid.NewGuid();
        var plainId = Guid.NewGuid();
        HasDocuments(
            NewDocument(
                eventId,
                signedId,
                required: true,
                displayOrder: 0,
                requiresSignature: true
            ),
            NewDocument(eventId, plainId, required: true, displayOrder: 1)
        );

        var result = await StateAsync();

        result
            .Documents.Select(d => (d.TermsDocumentId, d.RequiresSignature))
            .Should()
            .Equal((signedId, true), (plainId, false));
    }

    [Fact]
    public async Task HandleAsyncTellsTheVersionOfEachDocumentShown()
    {
        var rewrittenId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, rewrittenId, required: true, displayOrder: 0, version: 3),
            NewDocument(eventId, originalId, required: true, displayOrder: 1)
        );

        var result = await StateAsync();

        result
            .Documents.Select(d => (d.TermsDocumentId, d.Version))
            .Should()
            .Equal((rewrittenId, 3), (originalId, 1));
    }
}
