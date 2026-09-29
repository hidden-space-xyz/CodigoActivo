using AwesomeAssertions;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUserStatusTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListUserStatusTypesQueryHandler sut;

    public ListUserStatusTypesQueryHandlerTests()
    {
        sut = new ListUserStatusTypesQueryHandler(
            store,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleStatusTypesProjectsOrderedByName()
    {
        store.UserStatusTypes.AddRange([
            NewStatusTypeRow("Pending"),
            NewStatusTypeRow("Active"),
            NewStatusTypeRow("Blocked"),
        ]);

        var result = await sut.HandleAsync(
            new ListUserStatusTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(s => s.Name).Should().ContainInOrder("Active", "Blocked", "Pending");
        result.Should().AllBeOfType<UserStatusTypeResponse>();
    }
}
