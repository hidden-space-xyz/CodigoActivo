using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted partner domain entity and its relationships.
/// </summary>
public class Partner : AuditableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Gets or sets the from date value.
    /// </summary>
    public DateOnly FromDate { get; set; }
    /// <summary>
    /// Gets or sets the tier value.
    /// </summary>
    public int Tier { get; set; }
    /// <summary>
    /// Gets or sets the web value.
    /// </summary>
    public string? Web { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated thumbnail.
    /// </summary>
    public Guid ThumbnailId { get; set; }
    /// <summary>
    /// Gets or sets the associated thumbnail.
    /// </summary>
    public FileEntity Thumbnail { get; set; } = null!;
}
