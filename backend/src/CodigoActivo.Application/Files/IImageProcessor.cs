using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Turns an uploaded image into the variants the application stores, so no upload is ever
/// served as it was sent.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Decodes an image already detected as JPEG, PNG or WebP, applies its orientation, converts
    /// it to sRGB, drops every metadata block (EXIF, GPS, XMP, IPTC and the color profile) and
    /// encodes each <see cref="FileVariant"/> in <see cref="ProcessedImage.Format"/>.
    /// </summary>
    /// <param name="content">Content of the upload, positioned at its start.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the encoded variants, or
    /// <see cref="Common.Errors.ApplicationErrorCode.FileUploadDimensionsTooLarge"/> or
    /// <see cref="Common.Errors.ApplicationErrorCode.FileUploadInvalidImage"/>.
    /// </returns>
    public Task<Result<ProcessedImage>> ProcessAsync(
        Stream content,
        CancellationToken ct = default
    );
}

/// <summary>
/// Variants of an uploaded image, encoded and free of metadata.
/// </summary>
/// <param name="Full">Image at the size pages and rich text show it.</param>
/// <param name="Thumbnail">Smaller copy for lists, cards and logos.</param>
public sealed record ProcessedImage(byte[] Full, byte[] Thumbnail)
{
    /// <summary>
    /// Gets the format every variant is encoded in.
    /// </summary>
    public static ImageFormat Format => ImageFormat.Webp;

    /// <summary>
    /// Gets the encoded content of a variant.
    /// </summary>
    /// <param name="variant">Variant to get.</param>
    /// <returns>The encoded content.</returns>
    public byte[] Of(FileVariant variant)
    {
        return variant is FileVariant.Thumbnail ? Thumbnail : Full;
    }
}
