using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class PurgeUnusedFilesCommandHandlerTests
{
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly TestClock clock = new();
    private readonly PurgeUnusedFilesCommandHandler sut;

    public PurgeUnusedFilesCommandHandlerTests()
    {
        sut = new PurgeUnusedFilesCommandHandler(files, orphanCleaner, storage, clock);
        storage.ListWrittenUpTo(Arg.Any<DateTimeOffset>()).Returns([]);
    }

    [Fact]
    public async Task HandleAsyncHandsTheUploadsOlderThanTheRetentionToTheOrphanCleaner()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<StoredFileId> old = [StoredFileId.New(), StoredFileId.New()];
        files.ListIdsUploadedUpToAsync(Arg.Any<DateTimeOffset>(), ct).Returns(old);
        orphanCleaner.PurgeOrphanedAsync(old, ct).Returns(1);

        var result = await sut.HandleAsync(new PurgeUnusedFilesCommand(), ct);

        result.Value.Should().Be(1);
        await files
            .Received(1)
            .ListIdsUploadedUpToAsync(clock.UtcNow.AddDays(-StoredFile.UnusedRetentionDays), ct);
    }

    [Fact]
    public async Task HandleAsyncRemovesTheOldStoredContentNoFileRowOwns()
    {
        var ct = TestContext.Current.CancellationToken;
        var owned = NewFile(extension: "webp");
        var ownerless = Guid.NewGuid();
        storage
            .ListWrittenUpTo(clock.UtcNow - PurgeUnusedFilesCommandHandler.StrayContentAge)
            .Returns([
                $"{owned.Id}.webp",
                $"{owned.Id}.thumbnail.webp",
                $"{owned.Id}.jpg",
                $"{ownerless}.webp",
                $"{ownerless}.thumbnail.webp",
                $".{Guid.NewGuid():N}.tmp",
                "notes.txt",
            ]);
        files
            .ListByIdsAsync(
                Arg.Is<IReadOnlyCollection<StoredFileId>>(ids =>
                    ids.Count == 2
                    && ids.Contains(owned.Id)
                    && ids.Contains(StoredFileId.From(ownerless))
                ),
                ct
            )
            .Returns([owned]);

        await sut.HandleAsync(new PurgeUnusedFilesCommand(), ct);

        storage.DidNotReceive().Delete($"{owned.Id}.webp");
        storage.DidNotReceive().Delete($"{owned.Id}.thumbnail.webp");
        storage.Received(1).Delete($"{owned.Id}.jpg");
        storage.Received(1).Delete($"{ownerless}.webp");
        storage.Received(1).Delete($"{ownerless}.thumbnail.webp");
        storage
            .Received(1)
            .Delete(Arg.Is<string>(name => name.EndsWith(".tmp", StringComparison.Ordinal)));
        storage.Received(1).Delete("notes.txt");
    }

    [Fact]
    public async Task HandleAsyncNoOldStoredContentLoadsNoFileRows()
    {
        var ct = TestContext.Current.CancellationToken;

        await sut.HandleAsync(new PurgeUnusedFilesCommand(), ct);

        await files
            .DidNotReceiveWithAnyArgs()
            .ListByIdsAsync(Arg.Any<IReadOnlyCollection<StoredFileId>>(), ct);
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }
}
