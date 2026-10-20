namespace CodigoActivo.Application.Emails;

/// <summary>
/// Defines configuration values for manual email.
/// </summary>
public sealed class ManualEmailOptions
{
    /// <summary>
    /// Gets or sets the max recipients value.
    /// </summary>
    public int MaxRecipients { get; set; } = 500;

    /// <summary>
    /// Gets or sets the max attachments value.
    /// </summary>
    public int MaxAttachments { get; set; } = 10;

    /// <summary>
    /// Gets or sets the max attachments bytes value.
    /// </summary>
    public long MaxAttachmentsBytes { get; set; } = 8 * 1024 * 1024;
}
