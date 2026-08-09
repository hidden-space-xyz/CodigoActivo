using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListActivityRoleTypesQueryHandlerTests
{
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly ListActivityRoleTypesQueryHandler sut;

    public ListActivityRoleTypesQueryHandlerTests()
    {
        sut = new ListActivityRoleTypesQueryHandler(
            roleTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleRoleTypesOrdersByNameAndProjects()
    {
        roleTypes
            .Query()
            .Returns(
                new List<ActivityRoleType>
                {
                    new() { Name = "Zeta", Description = "z" },
                    new() { Name = "Alpha", Description = "a" },
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new ListActivityRoleTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zeta");
        result.Should().AllBeOfType<ActivityRoleTypeResponse>();
    }
}
