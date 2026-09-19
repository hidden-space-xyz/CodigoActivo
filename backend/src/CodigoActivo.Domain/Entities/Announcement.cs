using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted announcement domain entity and its relationships.
/// </summary>
public class Announcement : AuditableEntity, IFeaturable
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
    /// Gets or sets whether the item is highlighted as featured.
    /// </summary>
    public bool Featured { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated thumbnail.
    /// </summary>
    public Guid ThumbnailId { get; set; }

    /// <summary>
    /// Gets or sets the associated thumbnail.
    /// </summary>
    public FileEntity Thumbnail { get; set; } = null!;
}
