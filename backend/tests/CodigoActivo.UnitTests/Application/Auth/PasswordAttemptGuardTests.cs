using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class PasswordAttemptGuardTests
{
    private const string Correct = "password123";

    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakePasswordHasher hasher = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly RecordingLogger<PasswordAttemptGuard> logger = new();
    private readonly PasswordLockoutOptions options = new() { MaxFailedAttempts = 3 };
    private readonly PasswordAttemptGuard sut;

    public PasswordAttemptGuardTests()
    {
        sut = PasswordGuards.Create(
            hasher,
            uow,
            clock,
            sessions,
            emailSender,
            options,
            logger
        );
    }

    private static User Account()
    {
        return NewUser(passwordHash: FakePasswordHasher.Prefix + Correct);
    }

    private Task<int> AssertSessionsRevokedAsync(int times)
    {
        return sessions
            .Received(times)
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncCorrectPasswordAcceptsAndForgetsEarlierFailures()
    {
        var user = Account();
        user.PasswordFailedAttempts = 2;

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeFalse();
        await AssertSessionsRevokedAsync(0);
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncWrongPasswordCountsAndCommitsTheFailure()
    {
        var user = Account();

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordFailedAttempts.Should().Be(1);
        user.IsPasswordLocked().Should().BeFalse();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        emailSender.Sent.Should().BeEmpty();
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncReachingTheLimitLocksRevokesSessionsWarnsAndNotifies()
    {
        var user = Account();
        user.PasswordFailedAttempts = 2;

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "guessed-secret",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.IsPasswordLocked().Should().BeTrue();
        user.PasswordLockedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertSessionsRevokedAsync(1);

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be($"Account locked for user {user.Id} after 3 wrong passwords")
            .And.NotContain(Correct)
            .And.NotContain("guessed-secret")
            .And.NotContain(user.Email!);

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message.TextBody.Should().NotContain(Correct).And.NotContain("#userId=");
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncReachingTheLimitClosesThePendingChallenge()
    {
        var user = Account();
        user.PasswordFailedAttempts = 1;
        user.StartLoginChallenge(Guid.NewGuid());

        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);
        user.LoginChallengeId.Should().NotBeNull("the limit is not reached yet");

        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);

        user.IsPasswordLocked().Should().BeTrue();
        user.LoginChallengeId.Should().BeNull();
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncLockedAccountRefusesTheCorrectPasswordToo()
    {
        var user = Account();
        user.PasswordFailedAttempts = 2;
        await sut.VerifyLoginPasswordAsync(user, "wrong", TestContext.Current.CancellationToken);
        var lockedAt = user.PasswordLockedAt;

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.PasswordLockedAt.Should().Be(lockedAt);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertSessionsRevokedAsync(1);
        emailSender.Sent.Should().ContainSingle("the owner is only warned when the lock is set");
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncLockedAccountTreatsRightAndWrongPasswordsAlike()
    {
        var user = Account();
        user.PasswordLockedAt = clock.UtcNow.AddDays(-1);

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
        user.PasswordFailedAttempts.Should().Be(0);
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
        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeFalse();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncCorrectPasswordAcceptsAndForgetsEarlierFailures()
    {
        var user = Account();
        user.PasswordFailedAttempts = 2;

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeTrue();
        user.PasswordFailedAttempts.Should().Be(0);
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
        user.PasswordFailedAttempts.Should().Be(2);
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyReauthenticationAsyncLockedAccountRefusesTheCorrectPassword()
    {
        var user = Account();
        user.PasswordLockedAt = clock.UtcNow.AddMinutes(-5);

        var accepted = await sut.VerifyReauthenticationAsync(
            user,
            Correct,
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
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
        user.PasswordFailedAttempts.Should().Be(0);
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
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task VerifyLoginPasswordAsyncNotificationFailureStillLocksTheAccount()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = Account();
        user.PasswordFailedAttempts = 2;

        var accepted = await sut.VerifyLoginPasswordAsync(
            user,
            "wrong",
            TestContext.Current.CancellationToken
        );

        accepted.Should().BeFalse();
        user.IsPasswordLocked().Should().BeTrue();
        await AssertSessionsRevokedAsync(1);
    }
}
