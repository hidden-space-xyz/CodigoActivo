namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted link between an event and one of the terms documents that apply to
/// its signup, along with the per-event display and requirement settings for that document.
/// </summary>
public class EventTermsDocument
{
    /// <summary>
    /// Gets or sets the identifier of the associated event.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the associated event.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated terms document.
    /// </summary>
    public Guid TermsDocumentId { get; set; }

    /// <summary>
    /// Gets or sets the associated terms document.
    /// </summary>
    public TermsDocument TermsDocument { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether accepting this document is mandatory to complete a signup for the
    /// event.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the position in which this document is displayed among the event's terms.
    /// </summary>
    public int DisplayOrder { get; set; }
}
