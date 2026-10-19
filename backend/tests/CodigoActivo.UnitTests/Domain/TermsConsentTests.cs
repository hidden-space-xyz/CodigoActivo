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

public sealed class TermsConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = Now.AddDays(-2);
    private static readonly DateOnly Today = new(2026, 7, 4);

    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid guardianId = Guid.NewGuid();
    private readonly List<TermsDocumentId> consents = [];
    private int documentVersion = TermsDocument.FirstVersion;
    private User person;

    public TermsConsentTests()
    {
        person = Persisted.As<User>(new { Id = userId, Status = UserStatus.Active });
    }

    private void DecideFor(DateOnly birthDate)
    {
        person = Persisted.As<User>(
            new
            {
                Id = userId,
                Status = UserStatus.Dependent,
                ParentId = guardianId,
                BirthDate = birthDate,
            }
        );
    }

    private static IReadOnlyCollection<EventTermsDocument> Documents(
        params (Guid TermsDocumentId, bool Required)[] documents
    )
    {
        return Event
            .Create(
                new EventContent(
                    "Feria",
                    "Verano",
                    RichText.From("{}"),
                    StoredFileId.From(Guid.NewGuid())
                ),
                EventSchedule
                    .Create(
                        new DateOnly(2026, 8, 1),
                        new DateOnly(2026, 8, 3),
                        null,
                        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
                    )
                    .Value,
                EventCategorySelection.Create([EventCategoryTypeId.From(Guid.NewGuid())]).Value,
                EventTermsLinks
                    .Create(
                        [
                            .. documents.Select(document => new EventTermsLink(
                                TermsDocumentId.From(document.TermsDocumentId),
                                document.Required
                            )),
                        ],
                        []
                    )
                    .Value,
                UserId.From(Guid.NewGuid()),
                Earlier
            )
            .TermsDocuments;
    }

    private EventTermsAcceptance Stored(Guid termsDocumentId, bool accepted)
    {
        return EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(userId),
            UserId.From(userId),
            TermsDocumentId.From(termsDocumentId),
            1,
            accepted,
            null,
            UserId.From(userId),
            Earlier
        );
    }

    private Result<TermsConsentOutcome> Consent(
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions,
        params Guid[] signed
    )
    {
        return TermsConsent.Apply(
            EventId.From(eventId),
            person,
            documents,
            [.. signed.Select(TermsDocumentId.From)],
            consents,
            documents.ToDictionary(document => document.TermsDocumentId, _ => documentVersion),
            acceptances,
            decisions,
            UserId.From(userId),
            Today,
            Now
        );
    }

    private TermsConsentOutcome Apply(
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions,
        params Guid[] signed
    )
    {
        return Consent(documents, acceptances, decisions, signed).Value;
    }

    [Fact]
    public void ApplyDecisionForDocumentNotLinkedIgnoresIt()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [new TermsDecision(TermsDocumentId.From(Guid.NewGuid()), true)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyDecisionForDocumentNotLinkedDoesNotCountAsAcceptingARequiredOne()
    {
        var required = Guid.NewGuid();

        var outcome = Apply(
            Documents((required, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(Guid.NewGuid()), true)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeTrue();
    }

    [Fact]
    public void ApplyDecliningRequiredDocumentDoesNotRecordItAndReportsItMissing()
    {
        var required = Guid.NewGuid();

        var outcome = Apply(
            Documents((required, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(required), false)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeTrue();
    }

    [Fact]
    public void ApplyDecliningOptionalDocumentRecordsTheDecline()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [new TermsDecision(TermsDocumentId.From(optional), false)]
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.EventId.Value.Should().Be(eventId);
        recorded.UserId.Value.Should().Be(userId);
        recorded.TermsDocumentId.Value.Should().Be(optional);
        recorded.Accepted.Should().BeFalse();
        recorded.DecidedAt.Should().Be(Now);
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyAcceptingDocumentDeclinedBeforeAcceptsTheStoredDecision()
    {
        var required = Guid.NewGuid();
        var stored = Stored(required, false);

        var outcome = Apply(
            Documents((required, true)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(required), true)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
        stored.Accepted.Should().BeTrue();
        stored.DecidedAt.Should().Be(Now);
    }

    [Fact]
    public void ApplyRecordsTheCurrentVersionOfTheDocumentInTheDecisionAndItsHistory()
    {
        var optional = Guid.NewGuid();
        documentVersion = 3;

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [new TermsDecision(TermsDocumentId.From(optional), true)]
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.TermsDocumentVersion.Should().Be(3);
        recorded.Changes.Should().ContainSingle().Which.TermsDocumentVersion.Should().Be(3);
    }

    [Fact]
    public void ApplyAcceptingDocumentDeclinedOnAnEarlierVersionRecordsTheCurrentOne()
    {
        var optional = Guid.NewGuid();
        var stored = Stored(optional, false);
        documentVersion = 2;

        Apply(
            Documents((optional, false)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(optional), true)]
        );

        stored.TermsDocumentVersion.Should().Be(2);
        stored.Changes.Select(change => change.TermsDocumentVersion).Should().Equal(1, 2);
    }

    [Fact]
    public void ApplyDecliningDocumentAcceptedBeforeKeepsTheAcceptance()
    {
        var required = Guid.NewGuid();
        var stored = Stored(required, true);

        var outcome = Apply(
            Documents((required, true)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(required), false)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
        stored.Accepted.Should().BeTrue();
        stored.DecidedAt.Should().Be(Earlier);
    }

    [Fact]
    public void ApplyNoDecisionForRequiredDocumentReportsItMissing()
    {
        var outcome = Apply(Documents((Guid.NewGuid(), true), (Guid.NewGuid(), false)), [], null);

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeTrue();
    }

    [Fact]
    public void ApplyNoDecisionForOptionalDocumentsReportsNothingMissing()
    {
        var outcome = Apply(Documents((Guid.NewGuid(), false)), [], null);

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyRequiredDocumentAcceptedBeforeNeedsNoNewDecision()
    {
        var required = Guid.NewGuid();

        var outcome = Apply(Documents((required, true)), [Stored(required, true)], []);

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyAcceptingEveryRequiredDocumentRecordsThemAndReportsNothingMissing()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var outcome = Apply(
            Documents((first, true), (second, true)),
            [],
            [
                new TermsDecision(TermsDocumentId.From(first), true),
                new TermsDecision(TermsDocumentId.From(second), true),
            ]
        );

        outcome
            .Recorded.Select(acceptance => (acceptance.TermsDocumentId, acceptance.Accepted))
            .Should()
            .Equal((TermsDocumentId.From(first), true), (TermsDocumentId.From(second), true));
        outcome.Recorded.Should().OnlyContain(acceptance => acceptance.DecidedAt == Now);
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyDeclineThenAcceptOfSameOptionalDocumentRecordsOneAcceptance()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [
                new TermsDecision(TermsDocumentId.From(optional), false),
                new TermsDecision(TermsDocumentId.From(optional), true),
            ]
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.TermsDocumentId.Value.Should().Be(optional);
        recorded.Accepted.Should().BeTrue();
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyAcceptingDocumentThatAsksForSignatureKeepsTheSignature()
    {
        var required = Guid.NewGuid();
        var png = SignatureImages.Png();

        var outcome = Apply(
            Documents((required, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(required), true, png)],
            required
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.Accepted.Should().BeTrue();
        recorded.Signature!.ToArray().Should().Equal(png);
        outcome.MissingRequired.Should().BeFalse();
    }

    [Fact]
    public void ApplyAcceptingDocumentThatAsksForSignatureWithoutOneIsRefused()
    {
        var optional = Guid.NewGuid();

        var result = Consent(
            Documents((optional, false)),
            [],
            [new TermsDecision(TermsDocumentId.From(optional), true)],
            optional
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureRequired);
    }

    [Fact]
    public void ApplyAcceptingDocumentThatAsksForSignatureWithAnInvalidOneIsRefused()
    {
        var required = Guid.NewGuid();

        var result = Consent(
            Documents((required, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(required), true, new byte[] { 1, 2, 3 })],
            required
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureInvalid);
    }

    [Fact]
    public void ApplyRefusedSignatureLeavesEveryDecisionUnchanged()
    {
        var declined = Guid.NewGuid();
        var signed = Guid.NewGuid();
        var stored = Stored(declined, false);

        var result = Consent(
            Documents((declined, false), (signed, true)),
            [stored],
            [
                new TermsDecision(TermsDocumentId.From(declined), true),
                new TermsDecision(TermsDocumentId.From(signed), true),
            ],
            signed
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureRequired);
        stored.Accepted.Should().BeFalse();
        stored.DecidedAt.Should().Be(Earlier);
    }

    [Fact]
    public void ApplyDecliningDocumentThatAsksForSignatureNeedsNone()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [new TermsDecision(TermsDocumentId.From(optional), false, SignatureImages.Png())],
            optional
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.Accepted.Should().BeFalse();
        recorded.Signature.Should().BeNull();
    }

    [Fact]
    public void ApplySignatureForDocumentThatAsksForNoneIsNotKept()
    {
        var required = Guid.NewGuid();

        var outcome = Apply(
            Documents((required, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(required), true, SignatureImages.Png())]
        );

        outcome.Recorded.Should().ContainSingle().Which.Signature.Should().BeNull();
    }

    [Fact]
    public void ApplyAcceptingDeclinedDocumentThatAsksForSignatureKeepsTheSignature()
    {
        var optional = Guid.NewGuid();
        var stored = Stored(optional, false);
        var png = SignatureImages.Png();

        var outcome = Apply(
            Documents((optional, false)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(optional), true, png)],
            optional
        );

        outcome.Recorded.Should().BeEmpty();
        stored.Accepted.Should().BeTrue();
        stored.Signature!.ToArray().Should().Equal(png);
    }

    [Fact]
    public void ApplyDocumentAcceptedBeforeNeedsNoNewSignature()
    {
        var required = Guid.NewGuid();
        var stored = Stored(required, true);

        var outcome = Apply(
            Documents((required, true)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(required), true)],
            required
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeFalse();
        stored.DecidedAt.Should().Be(Earlier);
    }

    [Fact]
    public void ApplyConsentLinkedAsRequiredIsNeverRequiredAndRecordsItsDecline()
    {
        var consent = Guid.NewGuid();
        consents.Add(TermsDocumentId.From(consent));
        var documents = Documents((consent, true));

        var undecided = Apply(documents, [], null);
        var declined = Apply(
            documents,
            [],
            [new TermsDecision(TermsDocumentId.From(consent), false)]
        );

        undecided.MissingRequired.Should().BeFalse();
        declined.MissingRequired.Should().BeFalse();
        declined.Recorded.Should().ContainSingle().Which.Accepted.Should().BeFalse();
    }

    [Fact]
    public void ApplyForAnAdultRecordsThemAsWhoDecided()
    {
        var conditions = Guid.NewGuid();

        var outcome = Apply(
            Documents((conditions, true)),
            [],
            [new TermsDecision(TermsDocumentId.From(conditions), true)]
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.UserId.Value.Should().Be(userId);
        recorded.DecidedById.Value.Should().Be(userId);
    }

    [Theory]
    [InlineData(2016, 7, 4, false)]
    [InlineData(2012, 7, 5, false)]
    [InlineData(2012, 7, 4, true)]
    [InlineData(2009, 1, 1, true)]
    public void ApplyForADependentRecordsTheGuardianForConditionsAndTheMinorFromFourteenForConsents(
        int year,
        int month,
        int day,
        bool decidesOwnConsent
    )
    {
        DecideFor(new DateOnly(year, month, day));
        var conditions = Guid.NewGuid();
        var consent = Guid.NewGuid();
        consents.Add(TermsDocumentId.From(consent));

        var outcome = Apply(
            Documents((conditions, true), (consent, false)),
            [],
            [
                new TermsDecision(TermsDocumentId.From(conditions), true),
                new TermsDecision(TermsDocumentId.From(consent), true),
            ]
        );

        outcome.Recorded.Should().OnlyContain(recorded => recorded.UserId.Value == userId);
        outcome
            .Recorded.Single(recorded => recorded.TermsDocumentId.Value == conditions)
            .DecidedById.Value.Should()
            .Be(guardianId);
        outcome
            .Recorded.Single(recorded => recorded.TermsDocumentId.Value == consent)
            .DecidedById.Value.Should()
            .Be(decidesOwnConsent ? userId : guardianId);
    }

    [Fact]
    public void ApplyAcceptingAConsentDeclinedBeforeRecordsWhoAcceptsItNow()
    {
        DecideFor(new DateOnly(2012, 7, 4));
        var consent = Guid.NewGuid();
        consents.Add(TermsDocumentId.From(consent));
        var stored = EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(userId),
            UserId.From(guardianId),
            TermsDocumentId.From(consent),
            1,
            false,
            null,
            UserId.From(guardianId),
            Earlier
        );

        Apply(
            Documents((consent, false)),
            [stored],
            [new TermsDecision(TermsDocumentId.From(consent), true)]
        );

        stored.Accepted.Should().BeTrue();
        stored.DecidedById.Value.Should().Be(userId);
        stored.DecidedAt.Should().Be(Now);
    }
}
