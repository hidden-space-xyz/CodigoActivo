using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted file entity domain entity and its relationships.
/// </summary>
public class FileEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Gets or sets the extension value.
    /// </summary>
    public required string Extension { get; set; }

    /// <summary>
    /// Gets or sets the uploaded at value.
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }
    /// <summary>
    /// Gets or sets the uploaded by value.
    /// </summary>
    public Guid UploadedBy { get; set; }
}
