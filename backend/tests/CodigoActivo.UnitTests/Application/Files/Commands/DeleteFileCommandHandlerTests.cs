using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Caching;
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
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteFileCommandHandler sut;

    public DeleteFileCommandHandlerTests()
    {
        sut = new DeleteFileCommandHandler(files, uow, storage, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncFileMissingReturnsNotFound()
    {
        files.FileMissing();

        var result = await sut.HandleAsync(
            new DeleteFileCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.FileNotFound);
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task HandleAsyncFileStillInUseReturnsConflict()
    {
        var file = NewFile();
        files.FileFound(file);
        files.FileReferenced(true);

        var result = await sut.HandleAsync(
            new DeleteFileCommand(file.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.FileInUse);
        files.DidNotReceiveWithAnyArgs().Remove(NewFile());
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncNotInUseRemovesRowSavesDeletesStoredContentAndInvalidatesCache()
    {
        var file = NewFile(name: "gone.png", extension: "png");
        files.FileFound(file);
        files.FileReferenced(false);

        var result = await sut.HandleAsync(
            new DeleteFileCommand(file.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        files.Received(1).Remove(file);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.png");
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Files)
                )
            );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
