namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Defines configuration values for file storage.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the directory files are stored in, the volume mounted in the API container.
    /// </summary>
    public string RootPath { get; set; } = "/app/files";
}
