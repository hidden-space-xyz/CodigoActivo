using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivityModalityTypesQueryHandlerTests
{
    private readonly IActivityModalityTypeRepository modalityTypes =
        Substitute.For<IActivityModalityTypeRepository>();
    private readonly ListActivityModalityTypesQueryHandler sut;

    public ListActivityModalityTypesQueryHandlerTests()
    {
        sut = new ListActivityModalityTypesQueryHandler(
            modalityTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleModalityTypesOrdersByNameAndProjects()
    {
        modalityTypes
            .Query()
            .Returns(
                new List<ActivityModalityType>
                {
                    new() { Name = "Presencial" },
                    new() { Name = "Online" },
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new ListActivityModalityTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(m => m.Name).Should().ContainInOrder("Online", "Presencial");
        result.Should().AllBeOfType<ActivityModalityTypeResponse>();
    }
}
