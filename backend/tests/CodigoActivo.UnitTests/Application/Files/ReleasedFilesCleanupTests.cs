using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Files;

public sealed class ReleasedFilesCleanupTests
{
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly ReleasedFilesCleanup sut;

    public ReleasedFilesCleanupTests()
    {
        sut = new ReleasedFilesCleanup(orphanCleaner);
    }

    [Fact]
    public async Task HandleAsyncReleasedFilesRemovesEachOrphanOnce()
    {
        var shared = StoredFileId.New();
        var other = StoredFileId.New();

        await sut.HandleAsync(
            [
                new PartnerDeleted(PartnerId.New(), shared),
                new PartnerUpdated(PartnerId.New(), other, StoredFileId.New()),
                new PartnerDeleted(PartnerId.New(), shared),
                new PartnerCreated(PartnerId.New()),
            ],
            TestContext.Current.CancellationToken
        );

        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<StoredFileId>>(ids =>
                    ids.Count == 2 && ids.Contains(shared) && ids.Contains(other)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncNothingReleasedDoesNothing()
    {
        var thumbnailId = StoredFileId.New();

        await sut.HandleAsync(
            [
                new PartnerCreated(PartnerId.New()),
                new PartnerUpdated(PartnerId.New(), thumbnailId, thumbnailId),
            ],
            TestContext.Current.CancellationToken
        );

        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteOrphanedAsync(
                Arg.Any<IReadOnlyCollection<StoredFileId>>(),
                Arg.Any<CancellationToken>()
            );
    }
}
