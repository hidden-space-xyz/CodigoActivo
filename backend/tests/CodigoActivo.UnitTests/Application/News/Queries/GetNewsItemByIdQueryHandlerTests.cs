using AwesomeAssertions;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Common;
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
            new GetNewsItemByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
    }
}
