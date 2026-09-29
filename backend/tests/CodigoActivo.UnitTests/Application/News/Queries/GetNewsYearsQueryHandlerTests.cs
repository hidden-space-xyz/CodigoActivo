using AwesomeAssertions;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Queries;

public sealed class GetNewsYearsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetNewsYearsQueryHandler sut;

    public GetNewsYearsQueryHandlerTests()
    {
        sut = new GetNewsYearsQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncDuplicateYearsReturnsDistinctDescending()
    {
        store.News.AddRange([
            NewNewsItemRow(year: 2023),
            NewNewsItemRow(year: 2025),
            NewNewsItemRow(year: 2023),
            NewNewsItemRow(year: 2024),
        ]);

        var result = await sut.HandleAsync(
            new GetNewsYearsQuery(),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainInOrder(2025, 2024, 2023);
        result.Should().HaveCount(3);
    }
}
