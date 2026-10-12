using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;
using TwoFactorMethod = CodigoActivo.Application.Abstractions.Querying.ReadModel.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetLoginChallengeQueryHandlerTests
{
    private static readonly Guid ChallengeId = new("cccccccc-0000-0000-0000-000000000002");

    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new();
    private readonly GetLoginChallengeQueryHandler sut;

    public GetLoginChallengeQueryHandlerTests()
    {
        sut = new GetLoginChallengeQueryHandler(store, new FakeQueryExecutor(), clock, options);
    }

    private Task<Result<LoginChallengeResponse>> QueryAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetLoginChallengeQuery(
                new LoginChallengeKey(UserId.From(userId), LoginChallengeId.From(ChallengeId))
            ),
            TestContext.Current.CancellationToken
        );
    }

    private UserRow Store(UserRow user)
    {
        store.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsChallengeExpired()
    {
        var result = await QueryAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncChallengeNotOpenReturnsChallengeExpired()
    {
        var user = NewUserRow(email: "ana.ruiz@test.com");
        store.Users.Add(user);

        var result = await QueryAsync(user.Id);

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncEmailUserReturnsMaskedAddress()
    {
        var user = Store(
            NewUserRow(
                email: "ana.ruiz@test.com",
                loginChallengeId: ChallengeId,
                loginChallengeStartedAt: clock.UtcNow.AddMinutes(-1)
            )
        );

        var result = await QueryAsync(user.Id);

        result
            .Value.Should()
            .Be(new LoginChallengeResponse(TwoFactorMethod.Email, "a***@test.com"));
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsNoAddress()
    {
        var user = Store(
            NewUserRow(
                twoFactorMethod: TwoFactorMethod.Authenticator,
                loginChallengeId: ChallengeId,
                loginChallengeStartedAt: clock.UtcNow.AddMinutes(-1)
            )
        );

        var result = await QueryAsync(user.Id);

        result.Value.Should().Be(new LoginChallengeResponse(TwoFactorMethod.Authenticator, null));
    }
}
