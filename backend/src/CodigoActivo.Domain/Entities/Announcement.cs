using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Announcement : AuditableEntity, IFeaturable
{
    public required string Title { get; set; }
    public required string Subtitle { get; set; }

    public string Description { get; set; } = "{}";

    public bool Featured { get; set; }

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;
}
