namespace CodigoActivo.Domain.Entities.Abstractions;

/// <summary>
/// Represents the persisted auditable entity domain entity and its relationships.
/// </summary>
public abstract class AuditableEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the created by value.
    /// </summary>
    public Guid CreatedBy { get; set; }
    /// <summary>
    /// Gets or sets the updated by value.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}
