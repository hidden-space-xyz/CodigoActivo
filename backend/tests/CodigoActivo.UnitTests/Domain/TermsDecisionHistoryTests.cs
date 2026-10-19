using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class TermsDecisionHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly EventId eventId = EventId.New();
    private readonly UserId personId = UserId.New();
    private readonly UserId guardianId = UserId.New();
    private readonly UserId adminId = UserId.New();
    private readonly TermsDocumentId documentId = TermsDocumentId.New();

    private EventTermsAcceptance Decide(bool accepted, HandwrittenSignature? signature = null)
    {
        return EventTermsAcceptance.Record(
            eventId,
            personId,
            guardianId,
            documentId,
            1,
            accepted,
            signature,
            guardianId,
            Now
        );
    }

    [Fact]
    public void RecordKeepsTheDecisionAsTheFirstChange()
    {
        var signature = HandwrittenSignature.Create(SignatureImages.Png()).Value;

        var decision = Decide(accepted: true, signature);

        var change = decision.Changes.Should().ContainSingle().Subject;
        change.EventId.Should().Be(eventId);
        change.UserId.Should().Be(personId);
        change.TermsDocumentId.Should().Be(documentId);
        change.Accepted.Should().BeTrue();
        change.DecidedById.Should().Be(guardianId);
        change.RecordedById.Should().Be(guardianId);
        change.ChangedAt.Should().Be(Now);
        change.Signature.Should().BeSameAs(signature);
    }

    [Fact]
    public void WithdrawEndsTheConsentKeepsTheAcceptanceInTheHistoryAndRaisesTheWithdrawal()
    {
        var signature = HandwrittenSignature.Create(SignatureImages.Png()).Value;
        var decision = Decide(accepted: true, signature);

        var result = decision.Withdraw(personId, adminId, Now.AddDays(3));

        result.IsSuccess.Should().BeTrue();
        decision.Accepted.Should().BeFalse();
        decision.Signature.Should().BeNull();
        decision.DecidedById.Should().Be(personId);
        decision.DecidedAt.Should().Be(Now.AddDays(3));
        decision
            .Changes.Select(change =>
                (change.Accepted, change.DecidedById, change.RecordedById, change.ChangedAt)
            )
            .Should()
            .Equal((true, guardianId, guardianId, Now), (false, personId, adminId, Now.AddDays(3)));
        decision.Changes.First().Signature.Should().BeSameAs(signature);
        decision
            .PullDomainEvents()
            .Should()
            .Equal(new TermsConsentWithdrawn(eventId, personId, documentId, adminId));
        decision.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void WithdrawADecisionNotAcceptedIsRefusedAndChangesNothing()
    {
        var decision = Decide(accepted: false);

        var result = decision.Withdraw(personId, personId, Now.AddDays(1));

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.EventTermsConsentNotGiven);
        decision.Changes.Should().ContainSingle();
        decision.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void WithdrawKeepsTheVersionThatWasAccepted()
    {
        var decision = Decide(accepted: false);
        decision.Accept(personId, 2, null, personId, Now.AddDays(1));

        decision.Withdraw(personId, personId, Now.AddDays(2));

        decision.TermsDocumentVersion.Should().Be(2);
        decision
            .Changes.Select(change => (change.Accepted, change.TermsDocumentVersion))
            .Should()
            .Equal((false, 1), (true, 2), (false, 2));
    }

    [Fact]
    public void AcceptAfterAWithdrawalAddsANewAcceptanceToTheHistory()
    {
        var decision = Decide(accepted: true);
        decision.Withdraw(personId, personId, Now.AddDays(1));

        decision.Accept(personId, 1, null, personId, Now.AddDays(2));

        decision.Accepted.Should().BeTrue();
        decision.Changes.Select(change => change.Accepted).Should().Equal(true, false, true);
    }
}
