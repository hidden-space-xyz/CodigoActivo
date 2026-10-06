using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Queries;

public sealed class GetFileContentQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly GetFileContentQueryHandler sut;

    public GetFileContentQueryHandlerTests()
    {
        sut = new GetFileContentQueryHandler(
            new GetFileByIdQueryHandler(store, new FakeQueryExecutor()),
            storage
        );
    }

    [Fact]
    public async Task HandleAsyncMetadataMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetFileContentQuery(StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.FileNotFound);
        await storage
            .DidNotReceiveWithAnyArgs()
            .OpenReadAsync(string.Empty, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncKnownSignatureReturnsDetectedContentTypeAndRewindsStream()
    {
        var file = NewFileRow(name: "avatar.png");
        store.Files.Add(file);
        var stream = PngStream();
        storage.OpenReadAsync($"{file.Id}.png", Arg.Any<CancellationToken>()).Returns(stream);

        var result = await sut.HandleAsync(
            new GetFileContentQuery(StoredFileId.From(file.Id)),
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
        var file = NewFileRow();
        store.Files.Add(file);
        storage
            .OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JunkStream());

        var result = await sut.HandleAsync(
            new GetFileContentQuery(StoredFileId.From(file.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/octet-stream");
    }
}
