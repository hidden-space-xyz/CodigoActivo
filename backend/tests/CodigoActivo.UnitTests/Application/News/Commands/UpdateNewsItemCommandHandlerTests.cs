using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class UpdateNewsItemCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateNewsItemCommandHandler sut;

    public UpdateNewsItemCommandHandlerTests()
    {
        sut = new UpdateNewsItemCommandHandler(
            news,
            files,
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        news.Finds(null);
        var request = new UpdateNewsItemRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
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
        var newsItem = NewNewsItem();
        news.Finds(newsItem);
        files.ThumbnailExists(false);
        var request = new UpdateNewsItemRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(newsItem.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.NewsItemThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesPersistsAndInvalidatesCache()
    {
        var newsItem = NewNewsItem("Old", "OldSub");
        news.Finds(newsItem);
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateNewsItemRequest("  New  ", "  NewSub  ", "{\"y\":2}", thumbnailId);

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(newsItem.Id, request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        newsItem.Title.Should().Be("New");
        newsItem.Subtitle.Should().Be("NewSub");
        newsItem.Description.Should().Be("{\"y\":2}");
        newsItem.ThumbnailId.Should().Be(thumbnailId);
        newsItem.UpdatedBy.Should().Be(caller);
        newsItem.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.News)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedCleansUpPreviousFileAfterSave()
    {
        var newsItem = NewNewsItem();
        var previousThumbnailId = newsItem.ThumbnailId;
        news.Finds(newsItem);
        files.ThumbnailExists(true);
        var request = new UpdateNewsItemRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(newsItem.Id, request, Guid.NewGuid()),
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
        var newsItem = NewNewsItem();
        news.Finds(newsItem);
        files.ThumbnailExists(true);
        var request = new UpdateNewsItemRequest("Title", "Subtitle", "{}", newsItem.ThumbnailId);

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(newsItem.Id, request, Guid.NewGuid()),
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
        var newsItem = NewNewsItem();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        newsItem.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        news.Finds(newsItem);
        files.ThumbnailExists(true);
        var request = new UpdateNewsItemRequest(
            "Title",
            "Subtitle",
            $"{{\"b\":\"/api/files/{keptId}/content\"}}",
            newsItem.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateNewsItemCommand(newsItem.Id, request, Guid.NewGuid()),
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
