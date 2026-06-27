namespace CodigoActivo.Domain.Storage;

public sealed class FileStorageOptions
{
    public const long DefaultMaxSizeBytes = 5 * 1024 * 1024;

    public string RootPath { get; set; } = "files";

    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;
}
