using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Domain.Users;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts.Sessions;

public sealed class SessionsEndOnPasswordReplacedTests
{
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly SessionsEndOnPasswordReplaced sut;

    public SessionsEndOnPasswordReplacedTests()
    {
        sut = new SessionsEndOnPasswordReplaced(sessions);
    }

    [Fact]
    public async Task HandleAsyncPasswordChangedEndsEverySessionOfTheAccountOnly()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new PasswordChanged(userId), TestContext.Current.CancellationToken);

        await sessions.Received(1).EndAllAsync(userId, Arg.Any<CancellationToken>());
        await sessions
            .ReceivedWithAnyArgs(1)
            .EndAllAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncPasswordResetEndsEverySessionOfTheAccountOnly()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new PasswordReset(userId), TestContext.Current.CancellationToken);

        await sessions.Received(1).EndAllAsync(userId, Arg.Any<CancellationToken>());
        await sessions
            .ReceivedWithAnyArgs(1)
            .EndAllAsync(default, TestContext.Current.CancellationToken);
    }
}
