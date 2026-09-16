using System.Buffers.Binary;

namespace CodigoActivo.Application.Extensions;

/// <summary>
/// Provides reusable extension methods for stream.
/// </summary>
public static class StreamExtensions
{
    private const int HeaderSize = 32;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

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
            _ when IsJpeg(header) => new ImageFormat("jpg", "image/jpeg"),
            _ when IsPng(header) => new ImageFormat("png", "image/png"),
            _ when IsGif(header) => new ImageFormat("gif", "image/gif"),
            _ when IsWebp(header, length) => new ImageFormat("webp", "image/webp"),
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
        if (header.Length < 24 || !header[..8].SequenceEqual(PngSignature))
        {
            return false;
        }

        var ihdrLength = BinaryPrimitives.ReadUInt32BigEndian(header[8..12]);
        if (ihdrLength != 13 || !header[12..16].SequenceEqual("IHDR"u8))
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
        return width > 0 && height > 0;
    }

    private static bool IsGif(ReadOnlySpan<byte> header)
    {
        if (
            header.Length < 10
            || (!header[..6].SequenceEqual("GIF87a"u8) && !header[..6].SequenceEqual("GIF89a"u8))
        )
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
        return width > 0 && height > 0;
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
public sealed record ImageFormat(string Extension, string ContentType);
