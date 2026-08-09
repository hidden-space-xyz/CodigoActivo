using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Files;

public sealed class OrphanFileCleanerTests
{
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ILocalFileSystemRepository storage =
        Substitute.For<ILocalFileSystemRepository>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly OrphanFileCleaner sut;

    public OrphanFileCleanerTests()
    {
        sut = new OrphanFileCleaner(
            files,
            uow,
            storage,
            cacheInvalidator,
            NullLogger<OrphanFileCleaner>.Instance
        );
    }

    private static FileEntity NewFile(string name = "photo.png", string extension = "png")
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Extension = extension,
            UploadedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UploadedBy = Guid.NewGuid(),
        };
    }

    private void FileFound(FileEntity file)
    {
        files.Finds(file);
    }

    private void FileMissing()
    {
        files.Finds(null);
    }

    private void FileReferenced(bool referenced)
    {
        files.IsInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(referenced);
    }

    private void InUseFilesAre(params Guid[] inUse)
    {
        files
            .GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([.. inUse]);
    }

    private void StoredFilesAre(params FileEntity[] all)
    {
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<FileEntity, bool>>>();
                Assert.NotNull(predicate);
                List<FileEntity> matches = [.. all.Where(predicate.Compile().Invoke)];
                return matches;
            });
    }

    [Fact]
    public async Task DeleteIfOrphanedAsyncNoLongerReferencedDeletesFile()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(false);

        await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        files.Received(1).Remove(file);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.png");
    }

    [Fact]
    public async Task DeleteIfOrphanedAsyncStillInUseKeepsFileSilently()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(true);

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        files.DidNotReceiveWithAnyArgs().Remove(NewFile());
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteIfOrphanedAsyncFileMissingIgnoresSilently()
    {
        FileMissing();

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteIfOrphanedAsyncStorageThrowsSwallowsException()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(false);
        storage.When(s => s.Delete(Arg.Any<string>())).Do(_ => throw new IOException("locked"));

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteIfOrphanedAsyncCancelledPropagatesCancellation()
    {
        files
            .When(f =>
                f.FindAsync(
                    Arg.Any<Expression<Func<FileEntity, bool>>>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new OperationCanceledException());

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeleteOrphanedAsyncMixedCandidatesRemovesOrphansOnceAndDeletesStoredContent()
    {
        var inUseFile = NewFile(name: "used.png", extension: "png");
        var orphanPng = NewFile(name: "a.png", extension: "png");
        var orphanJpg = NewFile(name: "b.jpg", extension: "jpg");
        InUseFilesAre(inUseFile.Id);
        StoredFilesAre(inUseFile, orphanPng, orphanJpg);

        await sut.DeleteOrphanedAsync(
            [inUseFile.Id, orphanPng.Id, orphanJpg.Id, orphanPng.Id],
            TestContext.Current.CancellationToken
        );

        await files
            .Received(1)
            .GetInUseAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Count == 3),
                Arg.Any<CancellationToken>()
            );
        files.Received(1).Remove(orphanPng);
        files.Received(1).Remove(orphanJpg);
        files.DidNotReceive().Remove(inUseFile);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{orphanPng.Id}.png");
        storage.Received(1).Delete($"{orphanJpg.Id}.jpg");
        storage.DidNotReceive().Delete($"{inUseFile.Id}.png");
    }

    [Fact]
    public async Task DeleteOrphanedAsyncEmptyCandidatesDoesNotTouchRepository()
    {
        await sut.DeleteOrphanedAsync([], TestContext.Current.CancellationToken);

        await files
            .DidNotReceiveWithAnyArgs()
            .GetInUseAsync([], TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteOrphanedAsyncAllCandidatesInUseKeepsFiles()
    {
        var first = NewFile();
        var second = NewFile();
        InUseFilesAre(first.Id, second.Id);

        await sut.DeleteOrphanedAsync([first.Id, second.Id], TestContext.Current.CancellationToken);

        files.DidNotReceiveWithAnyArgs().Remove(NewFile());
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteOrphanedAsyncRepositoryThrowsSwallowsException()
    {
        files
            .When(f =>
                f.GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            )
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.DeleteOrphanedAsync([Guid.NewGuid()], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task DeleteOrphanedAsyncStorageThrowsSwallowsExceptionAfterRemoving()
    {
        var orphan = NewFile();
        InUseFilesAre();
        StoredFilesAre(orphan);
        storage.When(s => s.Delete(Arg.Any<string>())).Do(_ => throw new IOException("locked"));

        var act = async () =>
            await sut.DeleteOrphanedAsync([orphan.Id], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        files.Received(1).Remove(orphan);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteOrphanedAsyncCancelledPropagatesCancellation()
    {
        files
            .When(f =>
                f.GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            )
            .Do(_ => throw new OperationCanceledException());

        var act = async () =>
            await sut.DeleteOrphanedAsync([Guid.NewGuid()], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
