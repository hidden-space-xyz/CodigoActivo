using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.TermsDecisions;

/// <summary>
/// Decision about one terms document of an event for one person, with who took it: the person, or
/// their guardian for the conditions and, before the age of 14, the consents of a dependent. A
/// declined document can be accepted later; an accepted one stays accepted, with the signature it
/// was accepted with when the document asked for one, until a consent is withdrawn. Every change
/// is kept in <see cref="Changes"/>.
/// </summary>
public class EventTermsAcceptance : IAggregateRoot, IHasDomainEvents
{
    private readonly List<TermsDecisionChange> changes = [];
    private readonly List<IDomainEvent> domainEvents = [];

    private EventTermsAcceptance() { }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the person the decision is about.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the person who decided: the person themselves or their guardian.
    /// </summary>
    public UserId DecidedById { get; private set; }

    /// <summary>
    /// Gets the identifier of the terms document.
    /// </summary>
    public TermsDocumentId TermsDocumentId { get; private set; }

    /// <summary>
    /// Gets the version of the document the decision was last taken on.
    /// </summary>
    public int TermsDocumentVersion { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the document is accepted now.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Gets when the decision was last taken.
    /// </summary>
    public DateTimeOffset DecidedAt { get; private set; }

    /// <summary>
    /// Gets the signature drawn to accept the document, when it asked for one and is accepted now.
    /// </summary>
    public HandwrittenSignature? Signature { get; private set; }

    /// <summary>
    /// Gets every change of the decision, oldest first.
    /// </summary>
    public IReadOnlyCollection<TermsDecisionChange> Changes => changes;

    /// <summary>
    /// Records a decision.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person the decision is about.</param>
    /// <param name="decidedById">Identifier of the person who decides.</param>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="termsDocumentVersion">Version of the document the decision is taken on.</param>
    /// <param name="accepted">Whether the document is accepted.</param>
    /// <param name="signature">Signature drawn to accept it, if any; a declined document keeps none.</param>
    /// <param name="recordedById">Identifier of the account that records the decision.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new decision.</returns>
    public static EventTermsAcceptance Record(
        EventId eventId,
        UserId userId,
        UserId decidedById,
        TermsDocumentId termsDocumentId,
        int termsDocumentVersion,
        bool accepted,
        HandwrittenSignature? signature,
        UserId recordedById,
        DateTimeOffset now
    )
    {
        var decision = new EventTermsAcceptance
        {
            EventId = eventId,
            UserId = userId,
            DecidedById = decidedById,
            TermsDocumentId = termsDocumentId,
            TermsDocumentVersion = termsDocumentVersion,
            Accepted = accepted,
            Signature = accepted ? signature : null,
            DecidedAt = now,
        };
        decision.changes.Add(new TermsDecisionChange(decision, recordedById, now));
        return decision;
    }

    /// <summary>
    /// Accepts a document declined or withdrawn before.
    /// </summary>
    /// <param name="decidedById">Identifier of the person who accepts it now.</param>
    /// <param name="termsDocumentVersion">Version of the document accepted.</param>
    /// <param name="signature">Signature drawn to accept it, if the document asks for one.</param>
    /// <param name="recordedById">Identifier of the account that records the acceptance.</param>
    /// <param name="now">Current time.</param>
    public void Accept(
        UserId decidedById,
        int termsDocumentVersion,
        HandwrittenSignature? signature,
        UserId recordedById,
        DateTimeOffset now
    )
    {
        DecidedById = decidedById;
        TermsDocumentVersion = termsDocumentVersion;
        Accepted = true;
        Signature = signature;
        DecidedAt = now;
        changes.Add(new TermsDecisionChange(this, recordedById, now));
    }

    /// <summary>
    /// Withdraws a consent given before, on the version that was accepted, and raises
    /// <see cref="TermsConsentWithdrawn"/>. The acceptance it ends, with its signature, stays in
    /// <see cref="Changes"/>, since withdrawing never makes unlawful what was done before.
    /// </summary>
    /// <param name="decidedById">Identifier of the person who withdraws it.</param>
    /// <param name="recordedById">Identifier of the account that records the withdrawal.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Success, or <see cref="DomainErrorCode.EventTermsConsentNotGiven"/> when the document is not
    /// accepted now.
    /// </returns>
    public Result Withdraw(UserId decidedById, UserId recordedById, DateTimeOffset now)
    {
        if (!Accepted)
        {
            return Error.Conflict(DomainErrorCode.EventTermsConsentNotGiven);
        }

        DecidedById = decidedById;
        Accepted = false;
        Signature = null;
        DecidedAt = now;
        changes.Add(new TermsDecisionChange(this, recordedById, now));
        domainEvents.Add(new TermsConsentWithdrawn(EventId, UserId, TermsDocumentId, recordedById));
        return Result.Success();
    }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var pulled = domainEvents.ToList();
        domainEvents.Clear();
        return pulled;
    }
}
