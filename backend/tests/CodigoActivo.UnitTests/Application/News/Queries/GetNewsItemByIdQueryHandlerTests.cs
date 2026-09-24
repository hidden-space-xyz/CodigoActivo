using AwesomeAssertions;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class GetNewsItemByIdQueryHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly GetNewsItemByIdQueryHandler sut;

    public GetNewsItemByIdQueryHandlerTests()
    {
        sut = new GetNewsItemByIdQueryHandler(news, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        news.HasNews();

        var result = await sut.HandleAsync(
            new GetNewsItemByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
    }
}
