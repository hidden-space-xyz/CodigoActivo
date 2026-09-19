namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event terms acceptance domain entity and its relationships.
/// </summary>
public class EventTermsAcceptance
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
    /// Gets or sets the identifier of the associated user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the associated user.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated terms document.
    /// </summary>
    public Guid TermsDocumentId { get; set; }

    /// <summary>
    /// Gets or sets whether the user accepted the document. A recorded decision can also be a
    /// rejection, kept for audit purposes.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Gets or sets when the decision was made.
    /// </summary>
    public DateTimeOffset DecidedAt { get; set; }
}
