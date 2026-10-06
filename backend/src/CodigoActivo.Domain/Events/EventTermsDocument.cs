using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Terms document linked to an event, shown in a given position; part of the event aggregate.
/// </summary>
public class EventTermsDocument
{
    private EventTermsDocument() { }

    internal EventTermsDocument(
        EventId eventId,
        TermsDocumentId termsDocumentId,
        bool isRequired,
        int displayOrder
    )
    {
        EventId = eventId;
        TermsDocumentId = termsDocumentId;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the terms document.
    /// </summary>
    public TermsDocumentId TermsDocumentId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether accepting the document is required to sign up.
    /// </summary>
    public bool IsRequired { get; private set; }

    /// <summary>
    /// Gets the position of the document in the event.
    /// </summary>
    public int DisplayOrder { get; private set; }

    internal void Place(bool isRequired, int displayOrder)
    {
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }
}
