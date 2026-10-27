namespace CodigoActivo.Application.Emails;

/// <summary>
/// Limits of the emails administrators write, fixed in code.
/// </summary>
public sealed class ManualEmailOptions
{
    /// <summary>
    /// Gets or sets the most recipients one email may reach.
    /// </summary>
    public int MaxRecipients { get; set; } = 500;

    /// <summary>
    /// Gets or sets the most files one email may carry.
    /// </summary>
    public int MaxAttachments { get; set; } = 10;

    /// <summary>
    /// Gets or sets the most bytes the attachments of one email may add up to.
    /// </summary>
    public long MaxAttachmentsBytes { get; set; } = 8 * 1024 * 1024;
}
