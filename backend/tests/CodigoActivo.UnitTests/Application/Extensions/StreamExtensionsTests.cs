using System.Buffers.Binary;
using System.Text;
using CodigoActivo.Application.Extensions;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

/// <summary>
/// Unit tests for <see cref="StreamExtensions.DetectImageFormatAsync"/>. Each case builds a raw
/// header <c>byte[]</c>, wraps it in a stream and asserts the detected <see cref="ImageFormat"/>
/// (or <c>null</c>), covering every magic-number / dimension / chunk branch plus the non-seekable
/// length path.
/// </summary>
public sealed class StreamExtensionsTests
{
    // ---- Valid formats -----------------------------------------------------

    public static TheoryData<byte[], ImageFormat> ValidHeaders() =>
        new()
        {
            { Jpeg(0xE0), new ImageFormat("jpg", "image/jpeg") },
            { Jpeg(0xC0), new ImageFormat("jpg", "image/jpeg") }, // lower boundary of header[3]
            { Png(), new ImageFormat("png", "image/png") },
            { Gif("GIF87a"), new ImageFormat("gif", "image/gif") },
            { Gif("GIF89a"), new ImageFormat("gif", "image/gif") },
            { Webp("VP8 "), new ImageFormat("webp", "image/webp") },
            { Webp("VP8L"), new ImageFormat("webp", "image/webp") },
            { Webp("VP8X"), new ImageFormat("webp", "image/webp") },
        };

    [Theory]
    [MemberData(nameof(ValidHeaders))]
    public async Task DetectImageFormatAsync_recognises_valid_headers(byte[] header, ImageFormat expected)
    {
        using var stream = new MemoryStream(header);

        var result = await stream.DetectImageFormatAsync();

        result.Should().Be(expected);
    }

    // ---- Rejected / unknown headers ---------------------------------------

    public static TheoryData<byte[]> RejectedHeaders() =>
        new()
        {
            new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, // unknown bytes
            new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG magic but too short (< 4)
            Jpeg(0xBF), // JPEG marker byte below 0xC0
            Truncate(Png(), 20), // PNG signature but header shorter than 24
            Png(width: 0), // PNG zero width
            Png(height: 0), // PNG zero height
            Png(ihdrLen: 12), // PNG wrong IHDR chunk length
            Png(tag: "IHDX"), // PNG wrong IHDR tag
            Truncate(Gif("GIF89a"), 8), // GIF magic but header shorter than 10
            Gif("GIF89a", width: 0, height: 0), // GIF zero dimensions
            Gif("GIF9999"), // GIF wrong magic
            Truncate(Webp("VP8 "), 12), // RIFF/WEBP but header shorter than 16
            Webp("XXXX"), // WEBP unknown chunk
            Webp("VP8 ", riffSize: 100), // WEBP riffSize larger than length - 8
        };

    [Theory]
    [MemberData(nameof(RejectedHeaders))]
    public async Task DetectImageFormatAsync_returns_null_for_unrecognised_headers(byte[] header)
    {
        using var stream = new MemoryStream(header);

        var result = await stream.DetectImageFormatAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectImageFormatAsync_returns_null_for_empty_stream()
    {
        using var stream = new MemoryStream([]);

        var result = await stream.DetectImageFormatAsync();

        result.Should().BeNull();
    }

    // ---- Non-seekable branch (length == read) -----------------------------

    [Fact]
    public async Task DetectImageFormatAsync_uses_bytes_read_as_length_when_stream_is_not_seekable()
    {
        // 20-byte WEBP: riffSize 12 <= (read 20) - 8, so it validates only if length == read.
        using var stream = new NonSeekableStream(Webp("VP8 ", riffSize: 12, length: 20));

        var result = await stream.DetectImageFormatAsync();

        result.Should().Be(new ImageFormat("webp", "image/webp"));
    }

    [Fact]
    public async Task DetectImageFormatAsync_rejects_oversized_webp_on_non_seekable_stream()
    {
        // riffSize 40 exceeds (read 20) - 8, so detection must fail without touching Length.
        using var stream = new NonSeekableStream(Webp("VP8 ", riffSize: 40, length: 20));

        var result = await stream.DetectImageFormatAsync();

        result.Should().BeNull();
    }

    // ---- Header builders ---------------------------------------------------

    private static byte[] Jpeg(byte fourth)
    {
        var b = new byte[32];
        b[0] = 0xFF;
        b[1] = 0xD8;
        b[2] = 0xFF;
        b[3] = fourth;
        return b;
    }

    private static byte[] Png(uint width = 16, uint height = 16, uint ihdrLen = 13, string tag = "IHDR")
    {
        var b = new byte[32];
        ReadOnlySpan<byte> sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(b);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8, 4), ihdrLen);
        Encoding.ASCII.GetBytes(tag).CopyTo(b.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20, 4), height);
        return b;
    }

    private static byte[] Gif(string magic, ushort width = 10, ushort height = 10)
    {
        var b = new byte[32];
        Encoding.ASCII.GetBytes(magic).CopyTo(b.AsSpan(0));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6, 2), width);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8, 2), height);
        return b;
    }

    private static byte[] Webp(string chunk, uint riffSize = 24, int length = 32)
    {
        var b = new byte[length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(b.AsSpan(0));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4, 4), riffSize);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(b.AsSpan(8));
        Encoding.ASCII.GetBytes(chunk).CopyTo(b.AsSpan(12));
        return b;
    }

    private static byte[] Truncate(byte[] source, int length) => source[..length];

    /// <summary>Read-only stream that reports <c>CanSeek == false</c> and throws on <c>Length</c>.</summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
