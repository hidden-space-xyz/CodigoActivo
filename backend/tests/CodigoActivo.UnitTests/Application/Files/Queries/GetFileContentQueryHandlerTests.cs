using AwesomeAssertions;
using CodigoActivo.Application.Files.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Queries;

public sealed class GetFileContentQueryHandlerTests
{
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly ILocalFileSystemRepository storage =
        Substitute.For<ILocalFileSystemRepository>();
    private readonly GetFileContentQueryHandler sut;

    public GetFileContentQueryHandlerTests()
    {
        sut = new GetFileContentQueryHandler(new GetFileByIdQueryHandler(files), storage);
    }

    [Fact]
    public async Task HandleAsyncMetadataMissingReturnsNotFound()
    {
        files.FileMissing();

        var result = await sut.HandleAsync(
            new GetFileContentQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.FileNotFound);
        await storage
            .DidNotReceiveWithAnyArgs()
            .OpenReadAsync(string.Empty, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncKnownSignatureReturnsDetectedContentTypeAndRewindsStream()
    {
        var file = NewFile(name: "avatar.png");
        files.FileFound(file);
        var stream = PngStream();
        storage.OpenReadAsync($"{file.Id}.png", Arg.Any<CancellationToken>()).Returns(stream);

        var result = await sut.HandleAsync(
            new GetFileContentQuery(file.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/png");
        result.Value.FileName.Should().Be("avatar.png");
        result.Value.Content.Should().BeSameAs(stream);
        result.Value.Content.Position.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncUnknownBytesFallsBackToOctetStream()
    {
        var file = NewFile();
        files.FileFound(file);
        storage
            .OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JunkStream());

        var result = await sut.HandleAsync(
            new GetFileContentQuery(file.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/octet-stream");
    }
}
