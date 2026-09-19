using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted resource domain entity and its relationships.
/// </summary>
public class Resource : AuditableEntity
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
    /// Gets or sets the url value.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated resource type.
    /// </summary>
    public Guid ResourceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the associated resource type.
    /// </summary>
    public ResourceType ResourceType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated thumbnail.
    /// </summary>
    public Guid ThumbnailId { get; set; }

    /// <summary>
    /// Gets or sets the associated thumbnail.
    /// </summary>
    public FileEntity Thumbnail { get; set; } = null!;
}
