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
}
