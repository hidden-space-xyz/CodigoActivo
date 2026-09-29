using AwesomeAssertions;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class ListAssignmentStatusTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListAssignmentStatusTypesQueryHandler sut;

    public ListAssignmentStatusTypesQueryHandlerTests()
    {
        sut = new ListAssignmentStatusTypesQueryHandler(
            store,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleStatusTypesOrdersByNameAndProjects()
    {
        store.AssignmentStatusTypes.AddRange([
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
        ]);

        var result = await sut.HandleAsync(
            new ListAssignmentStatusTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(s => s.Name).Should().ContainInOrder("Aprobado", "Confirmado");
        result.Should().AllBeOfType<AssignmentStatusTypeResponse>();
    }
}
