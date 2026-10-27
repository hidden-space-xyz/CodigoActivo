namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies how a stored binary part is presented in the delivered email.
/// </summary>
public enum EmailPartDisposition
{
    /// <summary>
    /// Sent as an attachment of the email.
    /// </summary>
    Attachment,

    /// <summary>
    /// Sent as an inline image the HTML body references through
    /// <see cref="EmailOutboxContentPart.InlineContentId"/>.
    /// </summary>
    Inline,
}

/// <summary>
/// Represents the persisted email outbox content part domain entity and its relationships. One row
/// carries one attachment or inline image of a stored email, so a message sent to many recipients
/// keeps a single copy of the bytes.
/// </summary>
public class EmailOutboxContentPart
{
    /// <summary>
    /// Gets the unique identifier, fixed when the row is created.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

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
    /// Gets or sets the file name the part is sent with.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type the part is sent with; one that cannot be parsed or that names a
    /// multipart or message type is sent as <c>application/octet-stream</c>.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier an inline image is referenced by from the HTML body;
    /// <see langword="null"/> for an attachment.
    /// </summary>
    public string? InlineContentId { get; set; }

    /// <summary>
    /// Gets or sets the protected bytes of the part.
    /// </summary>
    public byte[] Payload { get; set; } = [];

    /// <summary>
    /// Gets or sets the position the part keeps among the parts of its content: inline images first,
    /// then attachments, each in the order of the batch.
    /// </summary>
    public int DisplayOrder { get; set; }
}
