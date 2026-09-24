using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class SetNewsItemFeaturedCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly SetNewsItemFeaturedCommandHandler sut;

    public SetNewsItemFeaturedCommandHandlerTests()
    {
        sut = new SetNewsItemFeaturedCommandHandler(
            news,
            cacheInvalidator,
            new GetNewsItemByIdQueryHandler(news, new FakeQueryExecutor())
        );
    }

    [Fact]
    public async Task HandleAsyncIdMissingReturnsNotFound()
    {
        news.SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
    }

    [Fact]
    public async Task HandleAsyncMarkedReturnsFeaturedNewsItem()
    {
        var newsItem = NewNewsItem("Featured", featured: true);
        news.SetFeaturedAsync(newsItem.Id, Arg.Any<CancellationToken>()).Returns(true);
        news.HasNews(newsItem);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(newsItem.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(newsItem.Id);
        result.Value.Featured.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncMarkedInvalidatesNewsCache()
    {
        var newsItem = NewNewsItem("Featured", featured: true);
        news.SetFeaturedAsync(newsItem.Id, Arg.Any<CancellationToken>()).Returns(true);
        news.HasNews(newsItem);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(newsItem.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.News)
                )
            );
    }
}
