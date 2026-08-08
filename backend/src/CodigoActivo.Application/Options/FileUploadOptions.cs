namespace CodigoActivo.Application.Options;

public sealed class FileUploadOptions
{
    public const long DefaultMaxSizeBytes = 10 * 1024 * 1024;

    public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;
}
