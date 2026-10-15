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

    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

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
                    .Create([
                        .. documents.Select(document => new EventTermsLink(
                            TermsDocumentId.From(document.TermsDocumentId),
                            document.Required
                        )),
                    ])
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
            TermsDocumentId.From(termsDocumentId),
            accepted,
            null,
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
            UserId.From(userId),
            documents,
            [.. signed.Select(TermsDocumentId.From)],
            acceptances,
            decisions,
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
}
