using AwesomeAssertions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class GetUserByIdQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly GetUserByIdQueryHandler sut;

    public GetUserByIdQueryHandlerTests()
    {
        sut = new GetUserByIdQueryHandler(users, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsync_UserExists_ReturnsUser()
    {
        var user = NewUser();
        users.HasUsers(user);

        var result = await sut.HandleAsync(
            new GetUserByIdQuery(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Type.Should().NotBeNull();
        result.Value.Type.Name.Should().Be("Socio");
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNotFound()
    {
        users.HasUsers();

        var result = await sut.HandleAsync(
            new GetUserByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
    }
}
