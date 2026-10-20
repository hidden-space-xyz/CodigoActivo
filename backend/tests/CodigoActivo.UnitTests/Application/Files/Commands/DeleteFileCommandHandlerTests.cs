using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class DeleteFileCommandHandlerTests
{
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly DeleteFileCommandHandler sut;

    public DeleteFileCommandHandlerTests()
    {
        sut = new DeleteFileCommandHandler(files, orphanCleaner);
    }

    [Fact]
    public async Task HandleAsyncFileMissingReturnsNotFound()
    {
        files.FileMissing();

        var result = await sut.HandleAsync(
            new DeleteFileCommand(StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.FileNotFound);
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .PurgeOrphanedAsync([], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncFileStillInUseReturnsConflict()
    {
        var file = NewFile();
        files.FileFound(file);
        PurgeDeletes(0);

        var result = await sut.HandleAsync(
            new DeleteFileCommand(file.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.FileInUse);
    }

    [Fact]
    public async Task HandleAsyncNotInUsePurgesTheFile()
    {
        var file = NewFile();
        files.FileFound(file);
        PurgeDeletes(1);

        var result = await sut.HandleAsync(
            new DeleteFileCommand(file.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .PurgeOrphanedAsync(
                Arg.Is<IReadOnlyCollection<StoredFileId>>(ids =>
                    ids.Count == 1 && ids.Contains(file.Id)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    private void PurgeDeletes(int count)
    {
        orphanCleaner
            .PurgeOrphanedAsync(
                Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(count);
    }
}
