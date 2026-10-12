using AwesomeAssertions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task HandleAsyncSignedInUserReturnsTheirOwnAccount()
    {
        var store = new FakeReadStore();
        var user = NewUserRow(first: "Marta");
        store.Users.AddRange([NewUserRow(first: "Otra"), user]);
        var sut = new GetCurrentUserQueryHandler(
            new TestCurrentUser(UserId.From(user.Id)),
            new GetAccountQueryHandler(store, new FakeQueryExecutor())
        );

        var result = await sut.HandleAsync(
            new GetCurrentUserQuery(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.FirstName.Should().Be("Marta");
    }
}
