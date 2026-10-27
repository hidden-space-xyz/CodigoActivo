namespace CodigoActivo.Application.Files;

/// <summary>
/// Limits of image uploads, fixed in code: no deployment setting changes them.
/// </summary>
public sealed class FileUploadOptions
{
    /// <summary>
    /// Gets or sets the largest upload accepted, in bytes (10 MiB); a larger one is refused before
    /// it is read.
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
