namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for manual email.
/// </summary>
public sealed class ManualEmailOptions
{
    /// <summary>
    /// Identifies the default max recipients configuration or policy value.
    /// </summary>
    public const int DefaultMaxRecipients = 500;

    /// <summary>
    /// Identifies the default max attachments configuration or policy value.
    /// </summary>
    public const int DefaultMaxAttachments = 10;

    /// <summary>
    /// Identifies the default max attachments bytes configuration or policy value.
    /// </summary>
    public const long DefaultMaxAttachmentsBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the max recipients value.
    /// </summary>
    public int MaxRecipients { get; set; } = DefaultMaxRecipients;

    /// <summary>
    /// Gets or sets the max attachments value.
    /// </summary>
    public int MaxAttachments { get; set; } = DefaultMaxAttachments;

    /// <summary>
    /// Gets or sets the max attachments bytes value.
    /// </summary>
    public long MaxAttachmentsBytes { get; set; } = DefaultMaxAttachmentsBytes;
}
