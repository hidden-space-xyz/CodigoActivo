using AwesomeAssertions;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class ListResourceTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListResourceTypesQueryHandler sut;

    public ListResourceTypesQueryHandlerTests()
    {
        sut = new ListResourceTypesQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncTypesExistReturnsTypesOrderedByName()
    {
        store.ResourceTypes.AddRange([
            NewResourceTypeRow(isExternal: true, name: "Externo"),
            NewResourceTypeRow(name: "Interno"),
        ]);

        var result = await sut.HandleAsync(
            new ListResourceTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(t => t.Name).Should().ContainInOrder("Externo", "Interno");
        result.Select(t => t.IsExternal).Should().ContainInOrder(true, false);
    }
}
