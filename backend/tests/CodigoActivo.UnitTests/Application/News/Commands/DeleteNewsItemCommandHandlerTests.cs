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

public sealed class DeleteNewsItemCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly DeleteNewsItemCommandHandler sut;

    public DeleteNewsItemCommandHandlerTests()
    {
        sut = new DeleteNewsItemCommandHandler(news);
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        news.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteNewsItemCommand(NewsItemId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.NewsItemNotFound);
        news.DidNotReceiveWithAnyArgs().Remove(Arg.Any<NewsItem>());
    }

    [Fact]
    public async Task HandleAsyncImagesEmbeddedInDescriptionReleasesThemWithTheThumbnail()
    {
        var embeddedId = Guid.NewGuid();
        var newsItem = NewNewsItem(description: $"{{\"img\":\"/api/files/{embeddedId}/content\"}}");
        news.Finds(newsItem);

        var result = await sut.HandleAsync(
            new DeleteNewsItemCommand(newsItem.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        news.Received(1).Remove(newsItem);
        var deleted = newsItem
            .PullDomainEvents()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<NewsItemDeleted>()
            .Subject;
        deleted.NewsItemId.Should().Be(newsItem.Id);
        deleted
            .ReleasedFileIds.Should()
            .BeEquivalentTo([StoredFileId.From(embeddedId), newsItem.ThumbnailId]);
    }
}
