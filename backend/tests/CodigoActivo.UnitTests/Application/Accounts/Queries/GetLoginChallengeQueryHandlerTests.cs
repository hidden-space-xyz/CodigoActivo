using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;
using Gender = CodigoActivo.Application.Users.Contracts.Gender;
using TwoFactorMethod = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetLoginChallengeQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetLoginChallengeQueryHandler sut;

    public GetLoginChallengeQueryHandlerTests()
    {
        sut = new GetLoginChallengeQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<Result<LoginChallengeResponse>> QueryAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetLoginChallengeQuery(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsUnauthorized()
    {
        var result = await QueryAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncEmailUserReturnsMaskedAddress()
    {
        var user = NewUserRow(email: "ana.ruiz@test.com");
        store.Users.Add(user);

        var result = await QueryAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LoginChallengeResponse(TwoFactorMethod.Email, "a***@test.com"));
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsNoAddress()
    {
        var user = NewUserRow(twoFactorMethod: TwoFactorMethod.Authenticator);
        store.Users.Add(user);

        var result = await QueryAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new LoginChallengeResponse(TwoFactorMethod.Authenticator, null));
    }
}
