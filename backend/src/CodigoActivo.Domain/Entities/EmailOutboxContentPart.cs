using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Identifies how a stored binary part is presented in the delivered email.
/// </summary>
public enum EmailPartDisposition
{
    /// <summary>
    /// Selects the attachment option.
    /// </summary>
    Attachment,

    /// <summary>
    /// Selects the inline image option.
    /// </summary>
    Inline,
}

/// <summary>
/// Represents the persisted email outbox content part domain entity and its relationships. One row
/// carries one attachment or inline image of a stored email, so a message sent to many recipients
/// keeps a single copy of the bytes.
/// </summary>
public class EmailOutboxContentPart : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the identifier of the associated content.
    /// </summary>
    public Guid ContentId { get; set; }

    /// <summary>
    /// Gets or sets the associated content.
    /// </summary>
    public EmailOutboxContent Content { get; set; } = null!;

    /// <summary>
    /// Gets or sets how the part is presented in the delivered email.
    /// </summary>
    public EmailPartDisposition Disposition { get; set; }

    /// <summary>
    /// Gets or sets the file name value.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type value.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier an inline image is referenced by from the HTML body.
    /// </summary>
    public string? InlineContentId { get; set; }

    /// <summary>
    /// Gets or sets the protected bytes of the part.
    /// </summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>
    /// Gets or sets the position the part keeps among the parts of its content.
    /// </summary>
    public int DisplayOrder { get; set; }
}
