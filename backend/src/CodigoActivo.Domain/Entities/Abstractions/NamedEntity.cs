namespace CodigoActivo.Domain.Entities.Abstractions;

/// <summary>
/// Represents the persisted named entity domain entity and its relationships.
/// </summary>
public abstract class NamedEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public required string Description { get; set; }
}
