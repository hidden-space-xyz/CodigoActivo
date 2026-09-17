using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event domain entity and its relationships.
/// </summary>
public class Event : AuditableEntity, IFeaturable
{
    /// <summary>
    /// Gets or sets the title displayed to users.
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// Gets or sets the supporting subtitle displayed to users.
    /// </summary>
    public required string Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public string Description { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the date and time when the event starts.
    /// </summary>
    public DateOnly EventStartsAt { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the event ends.
    /// </summary>
    public DateOnly EventEndsAt { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the early signup starts.
    /// </summary>
    public DateTimeOffset? EarlySignupStartsAt { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the signup starts.
    /// </summary>
    public DateTimeOffset SignupStartsAt { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the signup ends.
    /// </summary>
    public DateTimeOffset SignupEndsAt { get; set; }

    /// <summary>
    /// Gets or sets whether the item is highlighted as featured.
    /// </summary>
    public bool Featured { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated thumbnail.
    /// </summary>
    public Guid ThumbnailId { get; set; }
    /// <summary>
    /// Gets or sets the associated thumbnail.
    /// </summary>
    public FileEntity Thumbnail { get; set; } = null!;

    /// <summary>
    /// Gets or sets the terms documents linked to this event, each with its own requirement and
    /// display order.
    /// </summary>
    public ICollection<EventTermsDocument> TermsDocuments { get; set; } = [];

    /// <summary>
    /// Gets or sets the related activities collection.
    /// </summary>
    public ICollection<Activity> Activities { get; set; } = [];
    /// <summary>
    /// Gets or sets the related categories collection.
    /// </summary>
    public ICollection<EventCategory> Categories { get; set; } = [];
    /// <summary>
    /// Gets or sets the related ratings collection.
    /// </summary>
    public ICollection<EventRating> Ratings { get; set; } = [];
}
