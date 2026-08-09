using AwesomeAssertions;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class ListResourceTypesQueryHandlerTests
{
    private readonly IResourceTypeRepository resourceTypes =
        Substitute.For<IResourceTypeRepository>();
    private readonly ListResourceTypesQueryHandler sut;

    public ListResourceTypesQueryHandlerTests()
    {
        sut = new ListResourceTypesQueryHandler(
            resourceTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncTypesExistReturnsTypesOrderedByName()
    {
        resourceTypes.HasTypes(
            NewResourceType(isExternal: true, name: "Externo"),
            NewResourceType(name: "Interno")
        );

        var result = await sut.HandleAsync(
            new ListResourceTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(t => t.Name).Should().ContainInOrder("Externo", "Interno");
        result.Select(t => t.IsExternal).Should().ContainInOrder(true, false);
    }
}
