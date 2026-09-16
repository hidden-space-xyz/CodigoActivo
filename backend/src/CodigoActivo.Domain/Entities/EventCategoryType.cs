using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event category type domain entity and its relationships.
/// </summary>
public class EventCategoryType : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// Gets or sets the related events collection.
    /// </summary>
    public ICollection<EventCategory> Events { get; set; } = [];
}
