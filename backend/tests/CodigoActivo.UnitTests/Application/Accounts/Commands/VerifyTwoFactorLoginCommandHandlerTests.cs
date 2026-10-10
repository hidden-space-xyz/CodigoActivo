using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class VerifyTwoFactorLoginCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 3 };
    private readonly RecordingLogger<VerifyTwoFactorLoginCommandHandler> logger = new();
    private readonly VerifyTwoFactorLoginCommandHandler sut;

    public VerifyTwoFactorLoginCommandHandlerTests()
    {
        sut = new VerifyTwoFactorLoginCommandHandler(
            users,
            uow.RunsTransactions(),
            clock,
            new OtpValidator(new FakeOneTimeCodeHasher()),
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            options,
            logger
        );
    }

    private Task<Result> VerifyAsync(Guid userId, string code)
    {
        return sut.HandleAsync(
            new VerifyTwoFactorLoginCommand(UserId.From(userId), code),
            TestContext.Current.CancellationToken
        );
    }

    private User Prepare(User user)
    {
        users.FindReturns(user);
        return user;
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await VerifyAsync(Guid.NewGuid(), "123456");

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenWithoutCountingTheAttempt()
    {
        var user = Prepare(NewUserWithLoginCode(clock));
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(5) });

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        user.TwoFactorFailedAttempts.Should().Be(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountIsRefusedLikeAnExpiredChallenge()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { PasswordLockedAt = clock.UtcNow.AddMinutes(-1) });

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        user.LastLoginAt.Should().BeNull();
        user.LoginCodeHash.Should().NotBeNull();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncCorrectEmailCodeCompletesLoginAndClearsTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 2 });

        var result = await VerifyAsync(user.Id.Value, " 123456 ");

        result.IsSuccess.Should().BeTrue();
        user.LoginCodeHash.Should().BeNull();
        user.LoginCodeExpiresAt.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.LastLoginAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncWrongEmailCodeCountsFailureAndKeepsTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        var result = await VerifyAsync(user.Id.Value, "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        user.LoginCodeHash.Should().NotBeNull("a wrong guess must not consume the code");
        user.LastLoginAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncExpiredEmailCodeIsRejected()
    {
        var user = Prepare(
            NewUserWithLoginCode(clock, code: "123456", expiresAt: clock.UtcNow.AddSeconds(-1))
        );

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
    }

    [Fact]
    public async Task HandleAsyncFailureLimitReachedLocksAndDiscardsTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = options.MaxFailedAttempts - 1 });

        var result = await VerifyAsync(user.Id.Value, "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserAcceptsFreshStepAndRemembersIt()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret, lastUsedStep: 41));
        totp.MatchStep(Secret, "222333", clock.UtcNow).Returns(42);

        var result = await VerifyAsync(user.Id.Value, "222333");

        result.IsSuccess.Should().BeTrue();
        user.AuthenticatorLastUsedStep.Should().Be(42);
        user.LastLoginAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserRejectsReplayedStep()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret, lastUsedStep: 42));
        totp.MatchStep(Secret, "222333", clock.UtcNow).Returns(42);

        var result = await VerifyAsync(user.Id.Value, "222333");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAcceptedCodeLogsNothing()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "123456");

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncRejectedCodeLogsNothingUntilTheLockout()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "000000");

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncFailureLimitReachedLogsTheLockoutWithoutTheUserId()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = options.MaxFailedAttempts - 1 });

        await VerifyAsync(user.Id.Value, "000000");

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be(
                $"The second factor of an account was locked after {options.MaxFailedAttempts} wrong codes"
            )
            .And.NotContain(user.Id.ToString());
    }

    [Fact]
    public async Task HandleAsyncChecksTheCodeWithTheAccountRowLocked()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "000000");

        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>()
            );
            _ = users.LockAsync(user, Arg.Any<CancellationToken>());
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsyncLockoutStoredByAParallelAttemptRefusesTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        users
            .LockAsync(user, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Persisted.Overwrite(
                    user,
                    new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(15) }
                );
                return true;
            });

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        user.LastLoginAt.Should().BeNull();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAccountGoneOnceLockedReturnsNotFound()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        users.LockAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserIgnoresEmailedCodes()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret));
        Persisted.Overwrite(
            user,
            new
            {
                LoginCodeHash = FakeOneTimeCodeHasher.Prefix + "123456",
                LoginCodeExpiresAt = clock.UtcNow.AddMinutes(5),
            }
        );
        totp.MatchStep(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns(default(long?));

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
    }
}
