using AwesomeAssertions;
using CodigoActivo.Domain.Events;
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
                new EventContent("Feria", "Verano", "{}", Guid.NewGuid()),
                EventSchedule
                    .Create(
                        new DateOnly(2026, 8, 1),
                        new DateOnly(2026, 8, 3),
                        null,
                        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
                    )
                    .Value,
                EventCategorySelection.Create([Guid.NewGuid()]).Value,
                EventTermsLinks
                    .Create([
                        .. documents.Select(document => new EventTermsLink(
                            document.TermsDocumentId,
                            document.Required
                        )),
                    ])
                    .Value,
                Guid.NewGuid(),
                Earlier
            )
            .TermsDocuments;
    }

    private EventTermsAcceptance Stored(Guid termsDocumentId, bool accepted)
    {
        return EventTermsAcceptance.Record(eventId, userId, termsDocumentId, accepted, Earlier);
    }

    private TermsConsentOutcome Apply(
        IReadOnlyCollection<EventTermsDocument> documents,
        IReadOnlyCollection<EventTermsAcceptance> acceptances,
        IReadOnlyList<TermsDecision>? decisions
    )
    {
        return TermsConsent.Apply(eventId, userId, documents, acceptances, decisions, Now);
    }

    [Fact]
    public void ApplyDecisionForDocumentNotLinkedIgnoresIt()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(
            Documents((optional, false)),
            [],
            [new TermsDecision(Guid.NewGuid(), true)]
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
            [new TermsDecision(Guid.NewGuid(), true)]
        );

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeTrue();
    }

    [Fact]
    public void ApplyDecliningRequiredDocumentDoesNotRecordItAndReportsItMissing()
    {
        var required = Guid.NewGuid();

        var outcome = Apply(Documents((required, true)), [], [new TermsDecision(required, false)]);

        outcome.Recorded.Should().BeEmpty();
        outcome.MissingRequired.Should().BeTrue();
    }

    [Fact]
    public void ApplyDecliningOptionalDocumentRecordsTheDecline()
    {
        var optional = Guid.NewGuid();

        var outcome = Apply(Documents((optional, false)), [], [new TermsDecision(optional, false)]);

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.EventId.Should().Be(eventId);
        recorded.UserId.Should().Be(userId);
        recorded.TermsDocumentId.Should().Be(optional);
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
            [new TermsDecision(required, true)]
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
            [new TermsDecision(required, false)]
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
            [new TermsDecision(first, true), new TermsDecision(second, true)]
        );

        outcome
            .Recorded.Select(acceptance => (acceptance.TermsDocumentId, acceptance.Accepted))
            .Should()
            .Equal((first, true), (second, true));
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
            [new TermsDecision(optional, false), new TermsDecision(optional, true)]
        );

        var recorded = outcome.Recorded.Should().ContainSingle().Which;
        recorded.TermsDocumentId.Should().Be(optional);
        recorded.Accepted.Should().BeTrue();
        outcome.MissingRequired.Should().BeFalse();
    }
}
