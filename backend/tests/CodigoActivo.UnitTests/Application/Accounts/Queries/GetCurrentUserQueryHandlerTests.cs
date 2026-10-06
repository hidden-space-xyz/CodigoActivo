using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetCurrentUserQueryHandler sut;

    public GetCurrentUserQueryHandlerTests()
    {
        sut = new GetCurrentUserQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsUnauthorized()
    {
        var result = await sut.HandleAsync(
            new GetCurrentUserQuery(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.CurrentUserNotFound);
    }

    [Fact]
    public async Task HandleAsyncUserExistsReturnsUserWithStatusAndWithoutType()
    {
        var user = NewUserRow(first: "Marta", statusName: "Activa");
        store.Users.AddRange([user, NewUserRow(first: "Otra")]);

        var result = await sut.HandleAsync(
            new GetCurrentUserQuery(UserId.From(user.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.FirstName.Should().Be("Marta");
        result
            .Value.Status.Should()
            .Be(new UserStatusResponse(user.UserStatusTypeId, "Activa", "#111"));
        result.Value.Type.Should().BeNull();
    }
}
