using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Domain.Users;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts.Sessions;

public sealed class SessionsEndOnPasswordReplacedOrLockedTests
{
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly SessionsEndOnPasswordReplacedOrLocked sut;

    public SessionsEndOnPasswordReplacedOrLockedTests()
    {
        sut = new SessionsEndOnPasswordReplacedOrLocked(sessions);
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedEndsEverySessionOfTheAccountOnly()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new PasswordLocked(userId), TestContext.Current.CancellationToken);

        await sessions.Received(1).EndAllAsync(userId, Arg.Any<CancellationToken>());
        await sessions
            .ReceivedWithAnyArgs(1)
            .EndAllAsync(default, TestContext.Current.CancellationToken);
    }
}
