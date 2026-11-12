using AwesomeAssertions;
using CodigoActivo.Application.Files;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Files;
using NetVips;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Seeders;

public sealed class DemoImageGeneratorTests
{
    [Fact]
    public async Task GenerateEncodesAnImageTheUploadProcessorAccepts()
    {
        using var processor = new VipsImageProcessor(new FileUploadOptions());
        await using var content = new MemoryStream(DemoImageGenerator.Generate(Guid.NewGuid()));

        var result = await processor.ProcessAsync(content, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GenerateDrawsAGradientAtTheSizeOfTheDemoImages()
    {
        using var image = Image.NewFromBuffer(DemoImageGenerator.Generate(Guid.NewGuid()));
        using var topLeft = image.Crop(0, 0, 1, 1);
        using var bottomRight = image.Crop(image.Width - 1, image.Height - 1, 1, 1);

        image.Width.Should().Be(DemoImageGenerator.Width);
        image.Height.Should().Be(DemoImageGenerator.Height);
        image.Bands.Should().Be(3);
        topLeft.Avg().Should().NotBeApproximately(bottomRight.Avg(), 10);
    }

    [Fact]
    public void GenerateGivesTheSameFileTheSameImage()
    {
        var fileId = Guid.NewGuid();

        DemoImageGenerator.Generate(fileId).Should().Equal(DemoImageGenerator.Generate(fileId));
    }
}
