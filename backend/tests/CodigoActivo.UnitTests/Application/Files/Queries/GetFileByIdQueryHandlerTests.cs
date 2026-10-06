using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files.Contracts;
using CodigoActivo.Application.Files.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Queries;

public sealed class GetFileByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetFileByIdQueryHandler sut;

    public GetFileByIdQueryHandlerTests()
    {
        sut = new GetFileByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncFileExistsReturnsItsMetadata()
    {
        var file = NewFileRow(name: "acta.pdf", extension: "pdf");
        store.Files.AddRange([NewFileRow(), file]);

        var result = await sut.HandleAsync(
            new GetFileByIdQuery(StoredFileId.From(file.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new FileResponse(file.Id, "acta.pdf", "pdf", file.UploadedAt));
    }

    [Fact]
    public async Task HandleAsyncFileMissingReturnsNotFound()
    {
        store.Files.Add(NewFileRow());

        var result = await sut.HandleAsync(
            new GetFileByIdQuery(StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.FileNotFound);
    }
}
