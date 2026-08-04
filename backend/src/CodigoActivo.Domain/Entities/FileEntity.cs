using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class FileEntity : IdentifiableEntity
{
    public required string Name { get; set; }
    public required string Extension { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}
