namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event rating submission domain entity and its relationships. It records
/// which user rated an event, without linking to the content of the anonymous rating itself, and
/// enforces at most one submission per user and event through its composite primary key.
/// </summary>
public class EventRatingSubmission
{
    /// <summary>
    /// Identifies the name of the database primary key constraint, used to translate a concurrent
    /// duplicate submission into a domain-friendly conflict instead of an unhandled exception.
    /// </summary>
    public const string PrimaryKeyConstraintName = "pk_event_rating_submissions";

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
}
