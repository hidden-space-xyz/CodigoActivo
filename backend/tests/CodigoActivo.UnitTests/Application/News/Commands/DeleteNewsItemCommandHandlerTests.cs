using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class DeleteNewsItemCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteNewsItemCommandHandler sut;

    public DeleteNewsItemCommandHandlerTests()
    {
        sut = new DeleteNewsItemCommandHandler(news, orphanCleaner, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        news.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteNewsItemCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
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
    public async Task HandleAsyncImagesEmbeddedInDescriptionCleansUpAndInvalidatesCache()
    {
        var newsItem = NewNewsItem();
        var embeddedId = Guid.NewGuid();
        newsItem.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        news.Finds(newsItem);

        var result = await sut.HandleAsync(
            new DeleteNewsItemCommand(newsItem.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Contains(embeddedId) && ids.Contains(newsItem.ThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.News)
                )
            );
    }
}
