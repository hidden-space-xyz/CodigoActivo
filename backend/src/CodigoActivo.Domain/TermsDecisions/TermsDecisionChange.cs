using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.TermsDecisions;

/// <summary>
/// One change of a decision about a terms document of an event for one person: an acceptance, with
/// the signature drawn for it, a decline or a withdrawal, with who decided, who recorded it and
/// when. The decision keeps every change, so a consent can be proven for the time it was given;
/// part of the decision aggregate.
/// </summary>
public class TermsDecisionChange
{
    private TermsDecisionChange() { }

    internal TermsDecisionChange(
        EventTermsAcceptance decision,
        UserId recordedById,
        DateTimeOffset changedAt
    )
    {
        EventId = decision.EventId;
        UserId = decision.UserId;
        TermsDocumentId = decision.TermsDocumentId;
        TermsDocumentVersion = decision.TermsDocumentVersion;
        Accepted = decision.Accepted;
        DecidedById = decision.DecidedById;
        RecordedById = recordedById;
        ChangedAt = changedAt;
        Signature = decision.Signature;
    }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the person the decision is about.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the terms document.
    /// </summary>
    public TermsDocumentId TermsDocumentId { get; private set; }

    /// <summary>
    /// Gets the version of the document the decision was taken on; a withdrawal keeps the version
    /// it withdraws.
    /// </summary>
    public int TermsDocumentVersion { get; private set; }

    /// <summary>
    /// Gets whether the document was accepted; <see langword="false"/> for a decline or a
    /// withdrawal.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Gets the identifier of the person who decided: the person themselves or their guardian.
    /// </summary>
    public UserId DecidedById { get; private set; }

    /// <summary>
    /// Gets the identifier of the account that recorded the change: whoever signed up, the holder
    /// or guardian who withdrew it, or the administrator who recorded a withdrawal asked by email.
    /// </summary>
    public UserId RecordedById { get; private set; }

    /// <summary>
    /// Gets when the change was made.
    /// </summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>
    /// Gets the signature drawn to accept the document, when it asked for one.
    /// </summary>
    public HandwrittenSignature? Signature { get; private set; }
}
