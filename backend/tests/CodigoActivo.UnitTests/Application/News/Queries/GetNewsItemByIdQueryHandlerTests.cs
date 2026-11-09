using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class GetNewsItemByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetNewsItemByIdQueryHandler sut;

    public GetNewsItemByIdQueryHandlerTests()
    {
        sut = new GetNewsItemByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncNewsItemMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetNewsItemByIdQuery(NewsItemId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.NewsItemNotFound);
    }
}
