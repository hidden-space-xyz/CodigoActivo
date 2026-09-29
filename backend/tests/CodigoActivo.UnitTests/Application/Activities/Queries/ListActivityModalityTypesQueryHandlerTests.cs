using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivityModalityTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListActivityModalityTypesQueryHandler sut;

    public ListActivityModalityTypesQueryHandlerTests()
    {
        sut = new ListActivityModalityTypesQueryHandler(
            store,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleModalityTypesOrdersByNameAndProjects()
    {
        store.ActivityModalityTypes.AddRange([
            new() { Name = "Presencial" },
            new() { Name = "Online" },
        ]);

        var result = await sut.HandleAsync(
            new ListActivityModalityTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(m => m.Name).Should().ContainInOrder("Online", "Presencial");
        result.Should().AllBeOfType<ActivityModalityTypeResponse>();
    }
}
