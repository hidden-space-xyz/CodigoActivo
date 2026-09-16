namespace CodigoActivo.Infrastructure.Storage;

/// <summary>
/// Defines configuration values for file storage.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the root path value.
    /// </summary>
    public string RootPath { get; set; } = "files";
}
