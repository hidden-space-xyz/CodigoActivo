using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Files;

public sealed class OrphanFileCleanerTests
{
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly OrphanFileCleaner sut;

    public OrphanFileCleanerTests()
    {
        sut = new OrphanFileCleaner(
            files,
            uow.RunsTransactionsOf<IReadOnlyList<StoredFile>>(),
            storage,
            NullLogger<OrphanFileCleaner>.Instance
        );
        files
            .LockForDeletionAsync(
                Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                (IReadOnlyList<StoredFileId>)[.. call.Arg<IReadOnlyCollection<StoredFileId>>()]
            );
    }

    private static StoredFile NewFile(string name = "photo.png", string extension = "png")
    {
        return Persisted.As<StoredFile>(
            new
            {
                Id = Guid.NewGuid(),
                Name = name,
                Extension = extension,
                UploadedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UploadedBy = Guid.NewGuid(),
            }
        );
    }

    private void InUseFilesAre(params Guid[] inUse)
    {
        files
            .GetInUseAsync(
                Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([.. inUse.Select(StoredFileId.From)]);
    }

    private void StoredFilesAre(params StoredFile[] all)
    {
        files
            .ListByIdsAsync(
                Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ci =>
            {
                var ids = ci.Arg<IReadOnlyCollection<StoredFileId>>();
                Assert.NotNull(ids);
                List<StoredFile> matches = [.. all.Where(file => ids.Contains(file.Id))];
                return matches;
            });
    }

    [Fact]
    public async Task DeleteOrphanedAsyncMixedCandidatesRemovesOrphansOnceAndDeletesStoredContent()
    {
        var inUseFile = NewFile(name: "used.png", extension: "png");
        var orphanPng = NewFile(name: "a.png", extension: "png");
        var orphanJpg = NewFile(name: "b.jpg", extension: "jpg");
        InUseFilesAre(inUseFile.Id.Value);
        StoredFilesAre(inUseFile, orphanPng, orphanJpg);

        var deleted = await sut.PurgeOrphanedAsync(
            [inUseFile.Id, orphanPng.Id, orphanJpg.Id, orphanPng.Id],
            TestContext.Current.CancellationToken
        );

        deleted.Should().Be(2);

        await files
            .Received(1)
            .GetInUseAsync(
                Arg.Is<IReadOnlyCollection<StoredFileId>>(ids => ids != null && ids.Count == 3),
                Arg.Any<CancellationToken>()
            );
        files.Received(1).Remove(orphanPng);
        files.Received(1).Remove(orphanJpg);
        files.DidNotReceive().Remove(inUseFile);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{orphanPng.Id}.png");
        storage.Received(1).Delete($"{orphanPng.Id}.thumbnail.png");
        storage.Received(1).Delete($"{orphanJpg.Id}.jpg");
        storage.Received(1).Delete($"{orphanJpg.Id}.thumbnail.jpg");
        storage.DidNotReceive().Delete($"{inUseFile.Id}.png");
        storage.DidNotReceive().Delete($"{inUseFile.Id}.thumbnail.png");
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
        InUseFilesAre(first.Id.Value, second.Id.Value);

        var deleted = await sut.PurgeOrphanedAsync(
            [first.Id, second.Id],
            TestContext.Current.CancellationToken
        );

        deleted.Should().Be(0);
        files.DidNotReceiveWithAnyArgs().Remove(NewFile());
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task PurgeOrphanedAsyncRepositoryThrowsLetsTheFailureThrough()
    {
        files
            .When(f =>
                f.LockForDeletionAsync(
                    Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.PurgeOrphanedAsync(
                [StoredFileId.From(Guid.NewGuid())],
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteOrphanedAsyncRepositoryThrowsSwallowsException()
    {
        files
            .When(f =>
                f.GetInUseAsync(
                    Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.DeleteOrphanedAsync(
                [StoredFileId.From(Guid.NewGuid())],
                TestContext.Current.CancellationToken
            );

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
    public async Task DeleteOrphanedAsyncSaveRefusedSwallowsExceptionAndKeepsContent()
    {
        var orphan = NewFile();
        InUseFilesAre();
        StoredFilesAre(orphan);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("refused"));

        var act = async () =>
            await sut.DeleteOrphanedAsync([orphan.Id], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        files.Received(1).Remove(orphan);
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteOrphanedAsyncTransactionNotRunTouchesNothingOutsideIt()
    {
        var orphan = NewFile();
        InUseFilesAre();
        StoredFilesAre(orphan);
        uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<IReadOnlyList<StoredFile>>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult<IReadOnlyList<StoredFile>>([]));

        await sut.DeleteOrphanedAsync([orphan.Id], TestContext.Current.CancellationToken);

        await files
            .DidNotReceiveWithAnyArgs()
            .GetInUseAsync([], TestContext.Current.CancellationToken);
        await files
            .DidNotReceiveWithAnyArgs()
            .ListByIdsAsync([], TestContext.Current.CancellationToken);
        files.DidNotReceiveWithAnyArgs().Remove(orphan);
        await AssertNotSavedAsync();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteOrphanedAsyncCandidatesAlreadyGoneDeletesNoContent()
    {
        InUseFilesAre();
        StoredFilesAre();

        await sut.DeleteOrphanedAsync([StoredFileId.New()], TestContext.Current.CancellationToken);

        files.DidNotReceiveWithAnyArgs().Remove(NewFile());
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    [Fact]
    public async Task DeleteOrphanedAsyncCancelledPropagatesCancellation()
    {
        files
            .When(f =>
                f.GetInUseAsync(
                    Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new OperationCanceledException());

        var act = async () =>
            await sut.DeleteOrphanedAsync(
                [StoredFileId.From(Guid.NewGuid())],
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
