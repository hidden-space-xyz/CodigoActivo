using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class FileEntity : IdentifiableEntity
{
    public string Name { get; set; } = null!;
    public string Extension { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}