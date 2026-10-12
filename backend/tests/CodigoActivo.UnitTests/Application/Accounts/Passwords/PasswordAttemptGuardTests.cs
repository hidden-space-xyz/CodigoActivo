using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Passwords;

public sealed class PasswordAttemptGuardTests
{
    private const string Correct = "password123";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakePasswordHasher hasher = new();
    private readonly PasswordLockoutOptions options = new() { MaxFailedAttempts = 3 };
    private readonly TestCurrentUser currentUser = new();
    private readonly PasswordAttemptGuard sut;

    public PasswordAttemptGuardTests()
    {
        sut = PasswordGuards.Create(hasher, uow, clock, options, users, currentUser);
    }

    private static User Account()
    {
        return NewUser(passwordHash: FakePasswordHasher.Prefix + Correct);
    }

    private Task AssertFailuresCountedAsync(int times)
    {
        return users
            .Received(times)
            .SavePasswordStateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncCorrectPasswordAcceptsAndForgetsEarlierFailures()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        user.PasswordLockout.IsLocked.Should().BeFalse();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncCorrectPasswordWithNothingCountedCommitsNothing()
    {
        var user = Account();

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncCorrectPasswordCommitsTheClearedCounter()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncWrongPasswordCountsTheFailureInTheDatabase()
    {
        var user = Account();

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(1);
        user.PasswordLockout.IsLocked.Should().BeFalse();
        await AssertFailuresCountedAsync(1);
        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
            _ = users.LockPasswordStateAsync(user, Arg.Any<CancellationToken>());
            _ = users.SavePasswordStateAsync(user, Arg.Any<CancellationToken>());
        });
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        user.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncRowLockNotObtainedCountsNothing()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });
        users.LockPasswordStateAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(2);
        user.PasswordLockout.IsLocked.Should().BeFalse();
        await AssertFailuresCountedAsync(0);
        user.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncReachingTheLimitHandsTheLockEventToTheSave()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });
        IReadOnlyList<PasswordLocked> saved = [];
        users
            .When(repository =>
                repository.SavePasswordStateAsync(user, Arg.Any<CancellationToken>())
            )
            .Do(_ => saved = user.PullDomainEvents<PasswordLocked>());

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "guessed-secret",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.IsLocked.Should().BeTrue();
        user.PasswordLockout.LockedAt.Should().Be(clock.UtcNow);
        await AssertFailuresCountedAsync(1);
        saved.Should().Equal(new PasswordLocked(user.Id));
        user.PullDomainEvents().Should().BeEmpty();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncReachingTheLimitClosesThePendingChallenge()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 1 } });
        user.StartLoginChallenge(LoginChallengeId.New(), clock.UtcNow);

        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);
        user.LoginChallenge.Id.Should().NotBeNull("the limit is not reached yet");

        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);

        user.PasswordLockout.IsLocked.Should().BeTrue();
        user.LoginChallenge.Id.Should().BeNull();
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncLockedAccountRefusesTheCorrectPasswordToo()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });
        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);
        var lockedAt = user.PasswordLockout.LockedAt;

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.LockedAt.Should().Be(lockedAt);
        await AssertFailuresCountedAsync(1);
        user.PullDomainEvents()
            .Should()
            .Equal([new PasswordLocked(user.Id)], "the lock is only raised when it is set");
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncLockedAccountTreatsRightAndWrongPasswordsAlike()
    {
        var user = Account();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { LockedAt = clock.UtcNow.AddDays(-1) } }
        );

        var withCorrect = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );
        var withWrong = await sut.VerifyLoginPasswordAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        withCorrect.Should().Be(withWrong).And.BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncUnknownIdentifierIsRefusedWithoutCounting()
    {
        var accepted = await sut.VerifyLoginPasswordAsync(
            null,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VerifyLoginPasswordAsyncAccountWithoutAPasswordIsRefusedWithoutCounting(
        string? storedHash
    )
    {
        var user = NewUser(passwordHash: storedHash);

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        user.PasswordLockout.IsLocked.Should().BeFalse();
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncCorrectPasswordAcceptsAndForgetsEarlierFailures()
    {
        var user = Account();
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncWrongPasswordSharesTheSameCounterAsTheLogin()
    {
        var user = Account();
        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(2);
        await AssertFailuresCountedAsync(2);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncLockedAccountRefusesTheCorrectPassword()
    {
        var user = Account();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { LockedAt = clock.UtcNow.AddMinutes(-5) } }
        );

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VerifyReauthenticationAsyncMissingPasswordIsRefusedWithoutCounting(
        string? password
    )
    {
        var user = Account();

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            password,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncUnknownActingUserIsRefusedWithoutCounting()
    {
        var accepted = await sut.VerifyReauthenticationAsync(
            null,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        await AssertFailuresCountedAsync(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyActingUserAsyncCallerPasswordAcceptsTheSignedInCaller()
    {
        var caller = users.FindReturns(Account());
        currentUser.Id = caller.Id;

        var accepted = await sut.VerifyActingUserAsync(
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        await users.Received(1).GetByIdAsync(caller.Id, Arg.Any<CancellationToken>());
        await AssertFailuresCountedAsync(0);
    }

    [Fact]
    public async Task VerifyActingUserAsyncWrongPasswordCountsTheCallersFailure()
    {
        var caller = users.FindReturns(Account());
        currentUser.Id = caller.Id;

        var accepted = await sut.VerifyActingUserAsync(
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        await users.Received(1).GetByIdAsync(caller.Id, Arg.Any<CancellationToken>());
        await AssertFailuresCountedAsync(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VerifyActingUserAsyncMissingPasswordIsRefusedWithoutLoadingTheCaller(
        string? password
    )
    {
        var accepted = await sut.VerifyActingUserAsync(
            password,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
    }
}
