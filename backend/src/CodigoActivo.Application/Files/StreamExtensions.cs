using System.Buffers.Binary;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Provides reusable extension methods for stream.
/// </summary>
public static class StreamExtensions
{
    private const int HeaderSize = 32;

    /// <summary>
    /// Detects the image format from the supplied content.
    /// </summary>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching image format, or <see langword="null"/> when it is not found.</returns>
    public static async Task<ImageFormat?> DetectImageFormatAsync(
        this Stream content,
        CancellationToken ct = default
    )
    {
        var buffer = new byte[HeaderSize];
        var read = await content.ReadAtLeastAsync(buffer, HeaderSize, false, ct);

        var header = buffer.AsSpan(0, read);
        var length = content.CanSeek ? content.Length : read;

        return length switch
        {
            _ when IsJpeg(header) => ImageFormat.Jpeg,
            _ when IsPng(header) => ImageFormat.Png,
            _ when IsWebp(header, length) => ImageFormat.Webp,
            _ => null,
        };
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header)
    {
        var hasStartOfImage = header.Length >= 4 && header[0] is 0xFF && header[1] is 0xD8;
        return hasStartOfImage && header[2] is 0xFF && header[3] >= 0xC0;
    }

    private static bool IsPng(ReadOnlySpan<byte> header)
    {
        return PngImage.TryReadSize(header, out _, out _);
    }

    private static bool IsWebp(ReadOnlySpan<byte> header, long length)
    {
        if (
            header.Length < 16
            || !header[..4].SequenceEqual("RIFF"u8)
            || !header[8..12].SequenceEqual("WEBP"u8)
        )
        {
            return false;
        }

        var chunk = header[12..16];
        var knownChunk =
            chunk.SequenceEqual("VP8 "u8)
            || chunk.SequenceEqual("VP8L"u8)
            || chunk.SequenceEqual("VP8X"u8);
        if (!knownChunk)
        {
            return false;
        }

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
        return riffSize <= length - 8;
    }
}

/// <summary>
/// Represents an image format value used by the application.
/// </summary>
/// <param name="Extension">The extension value.</param>
/// <param name="ContentType">The content type value.</param>
public sealed record ImageFormat(string Extension, string ContentType)
{
    /// <summary>
    /// Gets the JPEG format.
    /// </summary>
    public static ImageFormat Jpeg { get; } = new("jpg", "image/jpeg");

    /// <summary>
    /// Gets the PNG format.
    /// </summary>
    public static ImageFormat Png { get; } = new("png", "image/png");

    /// <summary>
    /// Gets the WebP format.
    /// </summary>
    public static ImageFormat Webp { get; } = new("webp", "image/webp");

    /// <summary>
    /// Finds the format a stored extension stands for.
    /// </summary>
    /// <param name="extension">Extension of a stored file.</param>
    /// <returns>The matching format, or <see langword="null"/> when it is not one of them.</returns>
    public static ImageFormat? FromExtension(string extension)
    {
        return new[] { Jpeg, Png, Webp }.FirstOrDefault(format =>
            string.Equals(format.Extension, extension, StringComparison.Ordinal)
        );
    }
}
