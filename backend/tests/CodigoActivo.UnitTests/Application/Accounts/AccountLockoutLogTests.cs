using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class AccountLockoutLogTests
{
    private readonly RecordingLogger<AccountLockoutLog> logger = new();
    private readonly AccountLockoutLog sut;

    public AccountLockoutLogTests()
    {
        sut = new AccountLockoutLog(
            new PasswordLockoutOptions { MaxFailedAttempts = 3 },
            new TwoFactorOptions { MaxFailedAttempts = 4 },
            logger
        );
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedRecordsTheLockoutWithoutTheAccount()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new PasswordLocked(userId), TestContext.Current.CancellationToken);

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("An account was locked after 3 wrong passwords")
            .And.NotContain(userId.ToString());
    }

    [Fact]
    public async Task HandleAsyncTwoFactorLockedRecordsTheLockoutWithoutTheAccount()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new TwoFactorLocked(userId), TestContext.Current.CancellationToken);

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("The second factor of an account was locked after 4 wrong codes")
            .And.NotContain(userId.ToString());
    }
}
