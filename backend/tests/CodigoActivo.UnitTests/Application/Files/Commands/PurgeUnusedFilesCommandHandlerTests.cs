using AwesomeAssertions;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class PurgeUnusedFilesCommandHandlerTests
{
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly TestClock clock = new();
    private readonly PurgeUnusedFilesCommandHandler sut;

    public PurgeUnusedFilesCommandHandlerTests()
    {
        sut = new PurgeUnusedFilesCommandHandler(files, orphanCleaner, clock);
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
}
