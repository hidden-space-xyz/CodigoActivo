using AwesomeAssertions;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUserTypesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListUserTypesQueryHandler sut;

    public ListUserTypesQueryHandlerTests()
    {
        sut = new ListUserTypesQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncMultipleUserTypesProjectsOrderedByName()
    {
        store.UserTypes.AddRange([
            NewUserTypeRow("Volunteer"),
            NewUserTypeRow("Admin"),
            NewUserTypeRow("Member"),
        ]);

        var result = await sut.HandleAsync(
            new ListUserTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(t => t.Name).Should().ContainInOrder("Admin", "Member", "Volunteer");
        result.Should().AllBeOfType<UserTypeResponse>();
    }
}
