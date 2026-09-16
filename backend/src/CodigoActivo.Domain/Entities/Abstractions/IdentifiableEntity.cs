namespace CodigoActivo.Domain.Entities.Abstractions;

/// <summary>
/// Represents the persisted identifiable entity domain entity and its relationships.
/// </summary>
public abstract class IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
