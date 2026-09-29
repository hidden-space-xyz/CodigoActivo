namespace CodigoActivo.Domain.Common;

/// <summary>
/// Represents the persisted identifiable entity domain entity and its relationships.
/// </summary>
public abstract class IdentifiableEntity
{
    /// <summary>
    /// Gets the unique identifier, fixed when the entity is created.
    /// </summary>
    public Guid Id { get; protected init; } = Guid.NewGuid();
}
