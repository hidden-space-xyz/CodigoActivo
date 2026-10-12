using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class CompleteLoginCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private static readonly LoginChallengeId Challenge = LoginChallengeId.New();

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly List<UserSession> added = [];
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 3 };
    private readonly CompleteLoginCommandHandler sut;

    public CompleteLoginCommandHandlerTests()
    {
        sessions.AddAsync(Arg.Do<UserSession>(added.Add), Arg.Any<CancellationToken>());
        sut = new CompleteLoginCommandHandler(
            users,
            sessions,
            uow.RunsTransactionsOf<Result<UserSessionId>>(),
            clock,
            SecondFactorGuards.Create(uow, clock, totp, options),
            options,
            new SessionLifetimeOptions { Lifetime = TimeSpan.FromHours(8) }
        );
    }

    private Task<Result<UserSessionId>> VerifyAsync(Guid userId, string code)
    {
        return sut.HandleAsync(
            new CompleteLoginCommand(new LoginChallengeKey(UserId.From(userId), Challenge), code),
            TestContext.Current.CancellationToken
        );
    }

    private User Prepare(User user)
    {
        user.StartLoginChallenge(Challenge, clock.UtcNow.AddMinutes(-1));
        users.FindReturns(user);
        return user;
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsChallengeExpired()
    {
        users.FindReturns(null);

        var result = await VerifyAsync(Guid.NewGuid(), "123456");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenWithoutCountingTheAttempt()
    {
        var user = Prepare(NewUserWithLoginCode(clock));
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(5) });

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
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
        DomainEvents.Raised<TwoFactorLocked>(user).Should().BeEmpty();
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
        user.LoginChallengeId.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.LastLoginAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeOpensASessionForItsLifetime()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        var result = await VerifyAsync(user.Id.Value, "123456");

        var session = added.Should().ContainSingle().Subject;
        result.Value.Should().Be(session.Id);
        session.UserId.Should().Be(user.Id);
        session.CreatedAt.Should().Be(clock.UtcNow);
        session.ExpiresAt.Should().Be(clock.UtcNow.AddHours(8));
        Received.InOrder(() =>
        {
            _ = users.LockAsync(user, Arg.Any<CancellationToken>());
            _ = sessions.PurgeExpiredAsync(clock.UtcNow, user.Id, Arg.Any<CancellationToken>());
            _ = sessions.AddAsync(session, Arg.Any<CancellationToken>());
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsyncAnotherChallengeReturnsChallengeExpired()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        var result = await sut.HandleAsync(
            new CompleteLoginCommand(
                new LoginChallengeKey(user.Id, LoginChallengeId.New()),
                "123456"
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        user.LoginChallengeId.Should().Be(Challenge);
        added.Should().BeEmpty();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncChallengeOlderThanItsLifetimeReturnsChallengeExpired()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(
            user,
            new { LoginChallengeStartedAt = clock.UtcNow - options.ChallengeLifetime }
        );

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        user.LastLoginAt.Should().BeNull();
        added.Should().BeEmpty();
    }

    public static TheoryData<UserStatus, string?> AccountsThatCannotSignIn()
    {
        return new()
        {
            { UserStatus.Pending, "fake:secret" },
            { UserStatus.Blocked, "fake:secret" },
            { UserStatus.Dependent, null },
            { UserStatus.Active, null },
        };
    }

    [Theory]
    [MemberData(nameof(AccountsThatCannotSignIn))]
    public async Task HandleAsyncAccountThatCannotSignInIsRefusedBeforeItsCodeIsChecked(
        UserStatus status,
        string? passwordHash
    )
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { Status = status, PasswordHash = passwordHash });

        var result = await VerifyAsync(user.Id.Value, "000000");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.LastLoginAt.Should().BeNull();
        added.Should().BeEmpty();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
    public async Task HandleAsyncAcceptedCodeRaisesNoLock()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "123456");

        DomainEvents.Raised<TwoFactorLocked>(user).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncRejectedCodeRaisesNoLockUntilTheLimit()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "000000");

        DomainEvents.Raised<TwoFactorLocked>(user).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncFailureLimitReachedRaisesTheLockBeforeItsSave()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = options.MaxFailedAttempts - 1 });
        IReadOnlyList<TwoFactorLocked> saved = [];
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => saved = DomainEvents.Raised<TwoFactorLocked>(user));

        await VerifyAsync(user.Id.Value, "000000");

        saved.Should().Equal(new TwoFactorLocked(user.Id));
        user.IsTwoFactorLocked(clock.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncChecksTheCodeWithTheAccountRowLocked()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id.Value, "000000");

        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<UserSessionId>>>>(),
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

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
        user.LastLoginAt.Should().BeNull();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAccountGoneOnceLockedReturnsChallengeExpired()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        users.LockAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var result = await VerifyAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
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
