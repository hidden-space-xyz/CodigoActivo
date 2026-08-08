using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Announcements.AnnouncementTestData;

namespace CodigoActivo.UnitTests.Application.Announcements.Commands;

public sealed class DeleteAnnouncementCommandHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteAnnouncementCommandHandler sut;

    public DeleteAnnouncementCommandHandlerTests()
    {
        sut = new DeleteAnnouncementCommandHandler(
            announcements,
            orphanCleaner,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsync_AnnouncementMissing_ReturnsNotFound()
    {
        announcements.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteAnnouncementCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteOrphanedAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task HandleAsync_ImagesEmbeddedInDescription_CleansUpAndInvalidatesCache()
    {
        var announcement = NewAnnouncement();
        var embeddedId = Guid.NewGuid();
        announcement.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        announcements.Finds(announcement);

        var result = await sut.HandleAsync(
            new DeleteAnnouncementCommand(announcement.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null
                    && ids.Contains(embeddedId)
                    && ids.Contains(announcement.ThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Announcements)
                )
            );
    }
}
