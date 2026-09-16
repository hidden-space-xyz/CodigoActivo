namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for file upload.
/// </summary>
public sealed class FileUploadOptions
{
    /// <summary>
    /// Identifies the default max size bytes configuration or policy value.
    /// </summary>
    public const long DefaultMaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the max size bytes value.
    /// </summary>
    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;
}
