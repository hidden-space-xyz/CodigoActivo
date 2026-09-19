namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event category domain entity and its relationships.
/// </summary>
public class EventCategory
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
    /// Gets or sets the identifier of the associated event category type.
    /// </summary>
    public Guid EventCategoryTypeId { get; set; }

    /// <summary>
    /// Gets or sets the associated event category type.
    /// </summary>
    public EventCategoryType EventCategoryType { get; set; } = null!;
}
