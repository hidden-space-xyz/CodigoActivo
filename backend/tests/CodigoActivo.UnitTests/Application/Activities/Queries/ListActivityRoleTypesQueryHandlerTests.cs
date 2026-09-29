using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivityRoleTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListActivityRoleTypesQueryHandler sut;

    public ListActivityRoleTypesQueryHandlerTests()
    {
        sut = new ListActivityRoleTypesQueryHandler(
            store,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleRoleTypesOrdersByNameAndProjects()
    {
        store.ActivityRoleTypes.AddRange([
            new() { Name = "Zeta", Description = "z" },
            new() { Name = "Alpha", Description = "a" },
        ]);

        var result = await sut.HandleAsync(
            new ListActivityRoleTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zeta");
        result.Should().AllBeOfType<ActivityRoleTypeResponse>();
    }
}
