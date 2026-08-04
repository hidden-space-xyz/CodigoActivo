using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class Partner : AuditableEntity
{
    public required string Name { get; set; }
    public DateOnly FromDate { get; set; }
    public int Tier { get; set; }
    public string? Web { get; set; }

    public Guid ThumbnailId { get; set; }
    public FileEntity Thumbnail { get; set; } = null!;
}
