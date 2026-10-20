namespace CodigoActivo.Application.Files;

/// <summary>
/// Defines configuration values for file upload.
/// </summary>
public sealed class FileUploadOptions
{
    /// <summary>
    /// Gets or sets the max size bytes value.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most pixels an uploaded image may declare, read from its header before it
    /// is decoded, so a small file cannot expand into a huge image.
    /// </summary>
    public long MaxPixels { get; set; } = 50_000_000;

    /// <summary>
    /// Gets or sets how many uploads are decoded and encoded at the same time; the others wait.
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 2;
}
