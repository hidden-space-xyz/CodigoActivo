using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted activity modality type domain entity and its relationships.
/// </summary>
public class ActivityModalityType : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the related activities collection.
    /// </summary>
    public ICollection<Activity> Activities { get; set; } = [];
}
