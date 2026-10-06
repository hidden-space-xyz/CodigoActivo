using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class UpdateNewsItemCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UpdateNewsItemCommandHandler sut;

    public UpdateNewsItemCommandHandlerTests()
    {
        sut = new UpdateNewsItemCommandHandler(news, files, currentUser, clock);
    }

    private static UpdateNewsItemCommand Command(
        NewsItemId newsItemId,
        StoredFileId thumbnailId,
        string title = "Title",
        string subtitle = "Subtitle",
        string description = "{}"
    )
    {
        return new UpdateNewsItemCommand(newsItemId, title, subtitle, description, thumbnailId);
    }

    private static NewsItemUpdated SingleUpdate(NewsItem newsItem)
    {
        return newsItem
            .PullDomainEvents()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<NewsItemUpdated>()
            .Subject;
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        news.Finds(null);

        var result = await sut.HandleAsync(
            Command(NewsItemId.New(), StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.NewsItemNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(Arg.Any<StoredFileId>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndKeepsTheItem()
    {
        var newsItem = NewNewsItem("Old");
        news.Finds(newsItem);
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            Command(newsItem.Id, StoredFileId.New(), "New"),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.NewsItemThumbnailNotFound);
        newsItem.Title.Should().Be("Old");
        newsItem.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncValidCommandReplacesContentByTheCurrentUser()
    {
        var newsItem = NewNewsItem("Old", "OldSub");
        news.Finds(newsItem);
        files.ThumbnailExists(true);
        var thumbnailId = StoredFileId.New();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            Command(newsItem.Id, thumbnailId, "  New  ", "  NewSub  ", "{\"y\":2}"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        newsItem.Title.Should().Be("New");
        newsItem.Subtitle.Should().Be("NewSub");
        newsItem.Description.Json.Should().Be("{\"y\":2}");
        newsItem.ThumbnailId.Should().Be(thumbnailId);
        newsItem.UpdatedBy.Should().Be(currentUser.Id);
        newsItem.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedReleasesThePreviousFile()
    {
        var newsItem = NewNewsItem();
        var previousThumbnailId = newsItem.ThumbnailId;
        news.Finds(newsItem);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            Command(newsItem.Id, StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        SingleUpdate(newsItem).ReleasedFileIds.Should().Equal(previousThumbnailId);
    }

    [Fact]
    public async Task HandleAsyncThumbnailUnchangedReleasesNothing()
    {
        var newsItem = NewNewsItem();
        news.Finds(newsItem);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            Command(newsItem.Id, newsItem.ThumbnailId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        SingleUpdate(newsItem).ReleasedFileIds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncImagesDroppedFromDescriptionReleasesDroppedKeepsRest()
    {
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var newsItem = NewNewsItem(
            description: $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}"
        );
        news.Finds(newsItem);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            Command(
                newsItem.Id,
                newsItem.ThumbnailId,
                description: $"{{\"b\":\"/api/files/{keptId}/content\"}}"
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        SingleUpdate(newsItem).ReleasedFileIds.Should().Equal(StoredFileId.From(removedId));
    }
}
