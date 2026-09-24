using AwesomeAssertions;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class GetNewsYearsQueryHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly GetNewsYearsQueryHandler sut;

    public GetNewsYearsQueryHandlerTests()
    {
        sut = new GetNewsYearsQueryHandler(news, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncDuplicateYearsReturnsDistinctDescending()
    {
        news.HasNews(
            NewNewsItem(year: 2023),
            NewNewsItem(year: 2025),
            NewNewsItem(year: 2023),
            NewNewsItem(year: 2024)
        );

        var result = await sut.HandleAsync(
            new GetNewsYearsQuery(),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainInOrder(2025, 2024, 2023);
        result.Should().HaveCount(3);
    }
}
