using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListAssignmentStatusTypesQueryHandlerTests
{
    private readonly IAssignmentStatusTypeRepository statuses =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly ListAssignmentStatusTypesQueryHandler sut;

    public ListAssignmentStatusTypesQueryHandlerTests()
    {
        sut = new ListAssignmentStatusTypesQueryHandler(
            statuses,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleStatusTypesOrdersByNameAndProjects()
    {
        statuses
            .Query()
            .Returns(
                new List<AssignmentStatusType>
                {
                    new()
                    {
                        Name = "Confirmado",
                        Description = "c",
                        Color = "#0f0",
                    },
                    new()
                    {
                        Name = "Aprobado",
                        Description = "a",
                        Color = "#00f",
                    },
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new ListAssignmentStatusTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(s => s.Name).Should().ContainInOrder("Aprobado", "Confirmado");
        result.Should().AllBeOfType<AssignmentStatusTypeResponse>();
    }
}
