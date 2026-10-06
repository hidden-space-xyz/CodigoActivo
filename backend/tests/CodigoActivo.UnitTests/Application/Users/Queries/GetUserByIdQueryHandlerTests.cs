using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class GetUserByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetUserByIdQueryHandler sut;

    public GetUserByIdQueryHandlerTests()
    {
        sut = new GetUserByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncUserExistsReturnsUser()
    {
        var user = NewUserRow();
        store.Users.Add(user);

        var result = await sut.HandleAsync(
            new GetUserByIdQuery(UserId.From(user.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Type.Should().NotBeNull();
        result.Value.Type.Name.Should().Be("Socio");
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetUserByIdQuery(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }
}
