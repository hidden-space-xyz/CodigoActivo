namespace CodigoActivo.Application.Options;

public sealed class ManualEmailOptions
{
    public const int DefaultMaxRecipients = 500;
    public const int DefaultMaxAttachments = 10;
    public const long DefaultMaxAttachmentsBytes = 8 * 1024 * 1024;

    public int MaxRecipients { get; set; } = DefaultMaxRecipients;

    public int MaxAttachments { get; set; } = DefaultMaxAttachments;

    public long MaxAttachmentsBytes { get; set; } = DefaultMaxAttachmentsBytes;
}
