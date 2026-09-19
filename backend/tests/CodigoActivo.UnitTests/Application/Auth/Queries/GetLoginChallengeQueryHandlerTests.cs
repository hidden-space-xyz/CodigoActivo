using AwesomeAssertions;
using CodigoActivo.Application.Auth.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Queries;

public sealed class GetLoginChallengeQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly GetLoginChallengeQueryHandler sut;

    public GetLoginChallengeQueryHandlerTests()
    {
        sut = new GetLoginChallengeQueryHandler(users);
    }

    private Task<Result<LoginChallengeResponse>> QueryAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetLoginChallengeQuery(userId),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await QueryAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncEmailUserReturnsMaskedAddress()
    {
        var user = NewUser(email: "ana.ruiz@test.com");
        users.GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await QueryAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LoginChallengeResponse(TwoFactorMethod.Email, "a***@test.com"));
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsNoAddress()
    {
        var user = NewUserWithAuthenticator("SECRET");
        users.GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await QueryAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new LoginChallengeResponse(TwoFactorMethod.Authenticator, null));
    }
}
