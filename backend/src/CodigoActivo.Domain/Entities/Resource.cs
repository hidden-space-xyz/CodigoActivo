using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Resource : AuditableEntity
{
    public string Title { get; set; } = null!;
    public string Subtitle { get; set; } = null!;

    public string Description { get; set; } = "{}";
    public string? Url { get; set; }

    public Guid ResourceTypeId { get; set; }
    public ResourceType ResourceType { get; set; } = null!;

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;
}
