using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted email outbox content domain entity and its relationships. One row holds
/// the text an email is made of, shared by every pending message that delivers it, and is removed
/// once the last of those messages is gone. Subject and bodies are stored protected because they
/// quote one-time codes and reset links that exist nowhere else in the database as plain text.
/// </summary>
public class EmailOutboxContent : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the protected subject payload.
    /// </summary>
    public byte[] Subject { get; set; } = [];

    /// <summary>
    /// Gets or sets the protected HTML body payload.
    /// </summary>
    public byte[] HtmlBody { get; set; } = [];

    /// <summary>
    /// Gets or sets the protected text body payload.
    /// </summary>
    public byte[] TextBody { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the content was stored.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the attachments and inline images of the email.
    /// </summary>
    public ICollection<EmailOutboxContentPart> Parts { get; set; } = [];
}
