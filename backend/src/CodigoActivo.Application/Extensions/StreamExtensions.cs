using System.Buffers.Binary;

namespace CodigoActivo.Application.Extensions;

public static class StreamExtensions
{
    private const int HeaderSize = 32;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static async Task<ImageFormat?> DetectImageFormatAsync(
        this Stream content,
        CancellationToken ct = default
    )
    {
        var buffer = new byte[HeaderSize];
        var read = await content.ReadAtLeastAsync(buffer, HeaderSize, false, ct);

        var header = buffer.AsSpan(0, read);
        var length = content.CanSeek ? content.Length : read;

        if (IsJpeg(header))
            return new ImageFormat("jpg", "image/jpeg");

        if (IsPng(header))
            return new ImageFormat("png", "image/png");

        if (IsGif(header))
            return new ImageFormat("gif", "image/gif");

        if (IsWebp(header, length))
            return new ImageFormat("webp", "image/webp");

        return null;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header)
    {
        return header.Length >= 4
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF
            && header[3] >= 0xC0;
    }

    private static bool IsPng(ReadOnlySpan<byte> header)
    {
        if (header.Length < 24 || !header[..8].SequenceEqual(PngSignature))
            return false;

        var ihdrLength = BinaryPrimitives.ReadUInt32BigEndian(header[8..12]);
        if (ihdrLength != 13 || !header[12..16].SequenceEqual("IHDR"u8))
            return false;

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
            return false;

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
        return riffSize <= length - 8;
    }
}

public sealed record ImageFormat(string Extension, string ContentType);
