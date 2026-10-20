using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using NetVips;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Encodes the stored variants of an upload with libvips. Only its JPEG, PNG and WebP loaders stay
/// enabled, so an upload never reaches another decoder of the library; the dimensions are read
/// from the header before any pixel is decoded, and at most
/// <see cref="FileUploadOptions.MaxConcurrentProcessing"/> uploads are processed at once. The full
/// variant fits in 2048 × 2048 pixels and the thumbnail is at most 800 pixels wide; neither is
/// ever enlarged. Images with an alpha channel, such as logos, are encoded losslessly.
/// </summary>
public sealed class VipsImageProcessor : IImageProcessor, IDisposable
{
    private const int FullMaxSize = 2048;
    private const int ThumbnailMaxWidth = 800;
    private const int LossyQuality = 80;
    private const string OutputProfile = "srgb";

    private static readonly string[] AllowedLoaders =
    [
        "VipsForeignLoadJpeg",
        "VipsForeignLoadPng",
        "VipsForeignLoadWebp",
    ];

    private readonly FileUploadOptions options;
    private readonly SemaphoreSlim slots;

    static VipsImageProcessor()
    {
        Operation.Block("VipsForeignLoad", true);
        foreach (var loader in AllowedLoaders)
        {
            Operation.Block(loader, false);
        }

        Cache.Max = 0;
        Log.SetLogHandler(
            "VIPS",
            Enums.LogLevelFlags.Warning
                | Enums.LogLevelFlags.Message
                | Enums.LogLevelFlags.Info
                | Enums.LogLevelFlags.Debug,
            static (_, _, _) => { }
        );
    }

    /// <summary>
    /// Initializes the processor with the upload limits.
    /// </summary>
    /// <param name="options">Limits of the uploads.</param>
    public VipsImageProcessor(FileUploadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        slots = new SemaphoreSlim(options.MaxConcurrentProcessing, options.MaxConcurrentProcessing);
    }

    /// <inheritdoc />
    public async Task<Result<ProcessedImage>> ProcessAsync(
        Stream content,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);

        byte[] upload;
        using (var buffer = new MemoryStream())
        {
            await content.CopyToAsync(buffer, ct);
            upload = buffer.ToArray();
        }

        await slots.WaitAsync(ct);
        try
        {
            return Process(upload);
        }
        finally
        {
            slots.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        slots.Dispose();
    }

    private Result<ProcessedImage> Process(byte[] upload)
    {
        try
        {
            using (
                var header = Image.NewFromBuffer(
                    upload,
                    access: Enums.Access.Sequential,
                    failOn: Enums.FailOn.Error
                )
            )
            {
                if ((long)header.Width * header.Height > options.MaxPixels)
                {
                    return Error.Validation(ApplicationErrorCode.FileUploadDimensionsTooLarge);
                }
            }

            using var loaded = Image.ThumbnailBuffer(
                upload,
                FullMaxSize,
                height: FullMaxSize,
                size: Enums.Size.Down,
                outputProfile: OutputProfile,
                failOn: Enums.FailOn.Error
            );
            using var full = loaded.CopyMemory();
            using var thumbnail = full.ThumbnailImage(
                ThumbnailMaxWidth,
                height: FullMaxSize,
                size: Enums.Size.Down,
                noRotate: true
            );
            return new ProcessedImage(Encode(full), Encode(thumbnail));
        }
        catch (VipsException)
        {
            return Error.Validation(ApplicationErrorCode.FileUploadInvalidImage);
        }
    }

    private static byte[] Encode(Image image)
    {
        return image.HasAlpha()
            ? image.WebpsaveBuffer(lossless: true, keep: Enums.ForeignKeep.None)
            : image.WebpsaveBuffer(q: LossyQuality, keep: Enums.ForeignKeep.None);
    }
}
