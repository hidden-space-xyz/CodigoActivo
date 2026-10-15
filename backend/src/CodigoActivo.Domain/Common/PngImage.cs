using System.Buffers.Binary;

namespace CodigoActivo.Domain.Common;

/// <summary>
/// Reads the header every PNG image starts with: the eight-byte signature followed by the IHDR
/// chunk, which holds the size of the image.
/// </summary>
public static class PngImage
{
    /// <summary>
    /// Bytes the header takes: the signature plus the length, type, width and height of IHDR.
    /// </summary>
    public const int HeaderLength = 24;

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Reads the size of the PNG image a content starts with.
    /// </summary>
    /// <param name="content">Content, or at least its first <see cref="HeaderLength"/> bytes.</param>
    /// <param name="width">Width in pixels, or zero when the content is not a PNG image.</param>
    /// <param name="height">Height in pixels, or zero when the content is not a PNG image.</param>
    /// <returns><see langword="true"/> when the content starts with a PNG header of a non-empty image.</returns>
    public static bool TryReadSize(ReadOnlySpan<byte> content, out uint width, out uint height)
    {
        width = 0;
        height = 0;
        if (
            content.Length < HeaderLength
            || !content[..8].SequenceEqual(Signature)
            || BinaryPrimitives.ReadUInt32BigEndian(content[8..12]) != 13
            || !content[12..16].SequenceEqual("IHDR"u8)
        )
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt32BigEndian(content[16..20]);
        height = BinaryPrimitives.ReadUInt32BigEndian(content[20..24]);
        return width > 0 && height > 0;
    }
}
