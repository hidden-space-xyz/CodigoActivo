using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

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
            uow,
            clock,
            new OtpValidator(clock, new FakePasswordHasher()),
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

    private Task<Result<CodigoActivo.Application.DTOs.UserResponse>> VerifyAsync(
        Guid userId,
        string code
    )
    {
        return sut.HandleAsync(
            new VerifyTwoFactorLoginCommand(userId, code),
            TestContext.Current.CancellationToken
        );
    }

    private User Prepare(User user)
    {
        users.FindReturns(user);
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id));
        return user;
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await VerifyAsync(Guid.NewGuid(), "123456");

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenWithoutCountingTheAttempt()
    {
        var user = Prepare(NewUserWithLoginCode(clock));
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(5);

        var result = await VerifyAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        user.TwoFactorFailedAttempts.Should().Be(0);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountIsRefusedLikeAnExpiredChallenge()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        user.PasswordLockedAt = clock.UtcNow.AddMinutes(-1);

        var result = await VerifyAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.TwoFactorChallengeExpired);
        user.LastLoginAt.Should().BeNull();
        user.LoginCodeHash.Should().NotBeNull();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                $"Operation VerifyTwoFactorLogin refused for user {user.Id} because the account "
                    + "password is locked"
            );
    }

    [Fact]
    public async Task HandleAsyncCorrectEmailCodeCompletesLoginAndClearsTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        user.TwoFactorFailedAttempts = 2;

        var result = await VerifyAsync(user.Id, " 123456 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
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

        var result = await VerifyAsync(user.Id, "000000");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
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

        var result = await VerifyAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
    }

    [Fact]
    public async Task HandleAsyncFailureLimitReachedLocksAndDiscardsTheCode()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));
        user.TwoFactorFailedAttempts = options.MaxFailedAttempts - 1;

        var result = await VerifyAsync(user.Id, "000000");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserAcceptsFreshStepAndRemembersIt()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret, lastUsedStep: 41));
        totp.MatchStep(Secret, "222333", clock.UtcNow).Returns(42);

        var result = await VerifyAsync(user.Id, "222333");

        result.IsSuccess.Should().BeTrue();
        user.AuthenticatorLastUsedStep.Should().Be(42);
        user.LastLoginAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserRejectsReplayedStep()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret, lastUsedStep: 42));
        totp.MatchStep(Secret, "222333", clock.UtcNow).Returns(42);

        var result = await VerifyAsync(user.Id, "222333");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncCompletedLoginLogsTheUserIdAndTheSecondFactorMethod()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id, "123456");

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry
            .Message.Should()
            .Be($"Login completed for user {user.Id} with second factor Email")
            .And.NotContain("123456");
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorLoginLogsItsOwnSecondFactorMethod()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret, lastUsedStep: 41));
        totp.MatchStep(Secret, "222333", clock.UtcNow).Returns(42);

        await VerifyAsync(user.Id, "222333");

        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be($"Login completed for user {user.Id} with second factor Authenticator");
    }

    [Fact]
    public async Task HandleAsyncRejectedCodeNeverLogsACompletedLogin()
    {
        var user = Prepare(NewUserWithLoginCode(clock, code: "123456"));

        await VerifyAsync(user.Id, "000000");

        logger.Entries.Should().NotContain(entry => entry.Contains("Login completed"));
        logger.LevelEntries.Should().OnlyContain(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserIgnoresEmailedCodes()
    {
        var user = Prepare(NewUserWithAuthenticator(Secret));
        user.LoginCodeHash = FakePasswordHasher.Prefix + "123456";
        user.LoginCodeExpiresAt = clock.UtcNow.AddMinutes(5);
        totp.MatchStep(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((long?)null);

        var result = await VerifyAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
    }
}
