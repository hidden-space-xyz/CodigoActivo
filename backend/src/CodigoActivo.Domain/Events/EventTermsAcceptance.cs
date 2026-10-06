using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Decision a person took about one terms document of an event. A declined document can be
/// accepted later; an accepted one stays accepted.
/// </summary>
public class EventTermsAcceptance : IAggregateRoot
{
    private EventTermsAcceptance() { }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the person who decided.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the terms document.
    /// </summary>
    public TermsDocumentId TermsDocumentId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the document was accepted.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Gets when the decision was taken.
    /// </summary>
    public DateTimeOffset DecidedAt { get; private set; }

    /// <summary>
    /// Records a decision.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person who decides.</param>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="accepted">Whether the document is accepted.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new decision.</returns>
    public static EventTermsAcceptance Record(
        EventId eventId,
        UserId userId,
        TermsDocumentId termsDocumentId,
        bool accepted,
        DateTimeOffset now
    )
    {
        return new EventTermsAcceptance
        {
            EventId = eventId,
            UserId = userId,
            TermsDocumentId = termsDocumentId,
            Accepted = accepted,
            DecidedAt = now,
        };
    }

    /// <summary>
    /// Accepts a document declined before.
    /// </summary>
    /// <param name="now">Current time.</param>
    public void Accept(DateTimeOffset now)
    {
        Accepted = true;
        DecidedAt = now;
    }
}
