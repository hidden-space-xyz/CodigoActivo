using CodigoActivo.Application.Auth.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth.Queries;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly GetCurrentUserQueryHandler sut;

    public GetCurrentUserQueryHandlerTests()
    {
        sut = new GetCurrentUserQueryHandler(users);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await sut.HandleAsync(
            new GetCurrentUserQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.CurrentUserNotFound);
    }
}
