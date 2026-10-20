using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.UnitTests.TestSupport;
using NetVips;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Files;

public sealed class VipsImageProcessorTests : IDisposable
{
    private static readonly string[] MetadataFields =
    [
        "exif-data",
        "xmp-data",
        "iptc-data",
        "icc-profile-data",
        "orientation",
    ];

    private readonly FileUploadOptions options = new();
    private readonly VipsImageProcessor sut;

    public VipsImageProcessorTests()
    {
        sut = new VipsImageProcessor(options);
    }

    public void Dispose()
    {
        sut.Dispose();
    }

    private static Image Solid(int width, int height, params double[] color)
    {
        using var black = Image.Black(width, height, bands: color.Length);
        using var colored = black + color;
        return colored.Cast(Enums.BandFormat.Uchar);
    }

    private static byte[] TaggedJpeg(int width, int height)
    {
        using var solid = Solid(width, height, 200, 30, 60);
        using var tagged = solid.Mutate(image =>
        {
            image.Set(GValue.GIntType, "orientation", 6);
            image.Set(GValue.GStrType, "exif-ifd0-Artist", "Persona Fotografiada");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitudeRef", "N");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitude", "40/1 25/1 0/1");
        });
        return tagged.JpegsaveBuffer(q: 90);
    }

    private async Task<ProcessedImage> ProcessAsync(byte[] upload)
    {
        await using var content = new MemoryStream(upload, writable: false);
        var result = await sut.ProcessAsync(content, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private async Task<Result<ProcessedImage>> ProcessResultAsync(byte[] upload)
    {
        await using var content = new MemoryStream(upload, writable: false);
        return await sut.ProcessAsync(content, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsyncJpegWithExifAppliesItsOrientationAndDropsEveryMetadataBlock()
    {
        var processed = await ProcessAsync(TaggedJpeg(400, 300));

        foreach (var variant in new[] { processed.Full, processed.Thumbnail })
        {
            using var image = Image.NewFromBuffer(variant);
            image.Get("vips-loader").Should().Be("webpload_buffer");
            image.Width.Should().Be(300);
            image.Height.Should().Be(400);
            image
                .GetFields()
                .Should()
                .NotContain(field =>
                    MetadataFields.Contains(field)
                    || field.StartsWith("exif-", StringComparison.Ordinal)
                );
        }
    }

    [Fact]
    public async Task ProcessAsyncLargeImageFitsTheFullVariantInTheBoxAndNarrowsTheThumbnail()
    {
        using var large = Solid(3000, 1500, 10, 120, 200);

        var processed = await ProcessAsync(large.PngsaveBuffer());

        using var full = Image.NewFromBuffer(processed.Full);
        using var thumbnail = Image.NewFromBuffer(processed.Thumbnail);
        (full.Width, full.Height).Should().Be((2048, 1024));
        (thumbnail.Width, thumbnail.Height).Should().Be((800, 400));
    }

    [Fact]
    public async Task ProcessAsyncSmallImageIsNeverEnlarged()
    {
        using var small = Solid(320, 200, 10, 120, 200);

        var processed = await ProcessAsync(small.WebpsaveBuffer());

        using var full = Image.NewFromBuffer(processed.Full);
        using var thumbnail = Image.NewFromBuffer(processed.Thumbnail);
        (full.Width, full.Height).Should().Be((320, 200));
        (thumbnail.Width, thumbnail.Height).Should().Be((320, 200));
    }

    [Fact]
    public async Task ProcessAsyncImageWithAlphaKeepsItLosslessly()
    {
        using var logo = Solid(64, 32, 12, 34, 56, 78);

        var processed = await ProcessAsync(logo.PngsaveBuffer());

        using var full = Image.NewFromBuffer(processed.Full);
        full.HasAlpha().Should().BeTrue();
        full.Getpoint(10, 10).Should().Equal(12, 34, 56, 78);
    }

    [Fact]
    public async Task ProcessAsyncCmykJpegIsConvertedToSrgb()
    {
        using var solid = Solid(64, 48, 0, 200, 200, 0);
        using var cmyk = solid.Copy(interpretation: Enums.Interpretation.Cmyk);

        var processed = await ProcessAsync(cmyk.JpegsaveBuffer());

        using var full = Image.NewFromBuffer(processed.Full);
        full.Bands.Should().Be(3);
        full.Interpretation.Should().Be(Enums.Interpretation.Srgb);
    }

    [Fact]
    public async Task ProcessAsyncMorePixelsThanTheLimitReturnsDimensionsTooLarge()
    {
        options.MaxPixels = 399;
        using var image = Solid(20, 20, 1, 2, 3);

        var result = await ProcessResultAsync(image.PngsaveBuffer());

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadDimensionsTooLarge);
    }

    [Fact]
    public async Task ProcessAsyncPixelsAtTheLimitAreAccepted()
    {
        options.MaxPixels = 400;
        using var image = Solid(20, 20, 1, 2, 3);

        var result = await ProcessResultAsync(image.PngsaveBuffer());

        result.IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string> UndecodableUploads()
    {
        return ["png-header-only", "gif", "tiff", "svg", "junk"];
    }

    [Theory]
    [MemberData(nameof(UndecodableUploads))]
    public async Task ProcessAsyncContentNoAllowedLoaderDecodesReturnsInvalidImage(string kind)
    {
        using var solid = Solid(16, 16, 1, 2, 3);
        var upload = kind switch
        {
            "png-header-only" => solid.PngsaveBuffer()[..33],
            "gif" => solid.GifsaveBuffer(),
            "tiff" => solid.TiffsaveBuffer(),
            "svg" => "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'/>"u8.ToArray(),
            _ => new byte[64],
        };

        var result = await ProcessResultAsync(upload);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadInvalidImage);
    }
}
