using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted resource type domain entity and its relationships.
/// </summary>
public class ResourceType : NamedEntity
{
    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// Gets or sets whether external.
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// Gets or sets the related resources collection.
    /// </summary>
    public ICollection<Resource> Resources { get; set; } = [];
}
