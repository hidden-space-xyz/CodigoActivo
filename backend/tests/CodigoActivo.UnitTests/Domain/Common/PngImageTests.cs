using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Common;

public sealed class PngImageTests
{
    [Fact]
    public void TryReadSizePngHeaderReadsWidthAndHeight()
    {
        PngImage
            .TryReadSize(SignatureImages.Png(640, 180), out var width, out var height)
            .Should()
            .BeTrue();

        width.Should().Be(640);
        height.Should().Be(180);
    }

    [Fact]
    public void TryReadSizeContentShorterThanTheHeaderFails()
    {
        PngImage
            .TryReadSize(SignatureImages.Png().AsSpan(0, 23), out var width, out var height)
            .Should()
            .BeFalse();

        width.Should().Be(0);
        height.Should().Be(0);
    }

    [Fact]
    public void TryReadSizeFirstChunkOtherThanIhdrFails()
    {
        var png = SignatureImages.Png();
        png[12] = (byte)'X';

        PngImage.TryReadSize(png, out _, out _).Should().BeFalse();
    }
}
