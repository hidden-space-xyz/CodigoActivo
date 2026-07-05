using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Announcement : AuditableEntity, IFeaturable
{
    public string Title { get; set; } = null!;
    public string Subtitle { get; set; } = null!;

    public string Description { get; set; } = "{}";

    public bool Featured { get; set; }

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;
}