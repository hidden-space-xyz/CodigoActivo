using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Announcements.AnnouncementTestData;

namespace CodigoActivo.UnitTests.Application.Announcements.Commands;

public sealed class UpdateAnnouncementCommandHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateAnnouncementCommandHandler sut;

    public UpdateAnnouncementCommandHandlerTests()
    {
        sut = new UpdateAnnouncementCommandHandler(
            announcements,
            files,
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncAnnouncementMissingReturnsNotFound()
    {
        announcements.Finds(null);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(_ => true, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequest()
    {
        var announcement = NewAnnouncement();
        announcements.Finds(announcement);
        files.ThumbnailExists(false);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(announcement.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesPersistsAndInvalidatesCache()
    {
        var announcement = NewAnnouncement("Old", "OldSub");
        announcements.Finds(announcement);
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateAnnouncementRequest(
            "  New  ",
            "  NewSub  ",
            "{\"y\":2}",
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(announcement.Id, request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        announcement.Title.Should().Be("New");
        announcement.Subtitle.Should().Be("NewSub");
        announcement.Description.Should().Be("{\"y\":2}");
        announcement.ThumbnailId.Should().Be(thumbnailId);
        announcement.UpdatedBy.Should().Be(caller);
        announcement.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Announcements)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedCleansUpPreviousFileAfterSave()
    {
        var announcement = NewAnnouncement();
        var previousThumbnailId = announcement.ThumbnailId;
        announcements.Finds(announcement);
        files.ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(announcement.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Count == 1 && ids.Contains(previousThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncThumbnailUnchangedDoesNotCleanUp()
    {
        var announcement = NewAnnouncement();
        announcements.Finds(announcement);
        files.ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest(
            "Title",
            "Subtitle",
            "{}",
            announcement.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(announcement.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Count == 0),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncImagesDroppedFromDescriptionCleansUpDroppedKeepsRest()
    {
        var announcement = NewAnnouncement();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        announcement.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        announcements.Finds(announcement);
        files.ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest(
            "Title",
            "Subtitle",
            $"{{\"b\":\"/api/files/{keptId}/content\"}}",
            announcement.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateAnnouncementCommand(announcement.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Contains(removedId) && !ids.Contains(keptId)
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
