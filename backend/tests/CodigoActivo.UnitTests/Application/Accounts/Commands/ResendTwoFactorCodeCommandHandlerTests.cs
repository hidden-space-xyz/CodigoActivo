using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ResendTwoFactorCodeCommandHandlerTests
{
    private static readonly LoginChallengeId Challenge = LoginChallengeId.New();

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly TwoFactorOptions options = new();
    private readonly ResendTwoFactorCodeCommandHandler sut;

    public ResendTwoFactorCodeCommandHandlerTests()
    {
        var accountEmails = new AccountEmails(
            emailSender,
            composer,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        sut = new ResendTwoFactorCodeCommandHandler(
            users,
            AccountLocks.Create(users, Substitute.For<IUnitOfWork>()),
            clock,
            options,
            new LoginCodeIssuer(
                new FakeOneTimeCodeHasher(),
                options,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private User Prepare(User user)
    {
        user.StartLoginChallenge(Challenge, clock.UtcNow.AddMinutes(-1));
        return users.FindReturns(user);
    }

    private Task<Result> ResendAsync(Guid userId)
    {
        return sut.HandleAsync(
            new ResendTwoFactorCodeCommand(new LoginChallengeKey(UserId.From(userId), Challenge)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsChallengeExpired()
    {
        users.FindReturns(null);

        var result = await ResendAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task HandleAsyncChallengeNotOpenReturnsChallengeExpiredWithoutEmailing()
    {
        var user = users.FindReturns(
            NewUserWithLoginCode(clock, lastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncChallengeOlderThanItsLifetimeReturnsChallengeExpiredWithoutEmailing()
    {
        var user = users.FindReturns(NewUser());
        user.StartLoginChallenge(Challenge, clock.UtcNow - options.ChallengeLifetime);

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountReturnsChallengeExpiredWithoutEmailing()
    {
        var user = Prepare(NewUser());
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { LockedAt = clock.UtcNow.AddMinutes(-1) } }
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncBlockedAccountReturnsChallengeExpiredWithoutEmailing()
    {
        var user = Prepare(NewUser());
        Persisted.Overwrite(user, new { Status = UserStatus.Blocked });

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.TwoFactorChallengeExpired);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflict()
    {
        var user = Prepare(NewUserWithAuthenticator("SECRET"));

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = Prepare(NewUserWithLoginCode(clock));
        Persisted.Overwrite(
            user,
            new { TwoFactorLockout = new { LockedUntil = clock.UtcNow.AddMinutes(1) } }
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = Prepare(NewUserWithLoginCode(clock, lastSentAt: clock.UtcNow.AddSeconds(-30)));

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncCodeSentByAParallelResendBeforeTheLockReturnsConflict()
    {
        var user = Prepare(NewUserWithLoginCode(clock, lastSentAt: clock.UtcNow.AddMinutes(-2)));
        users.ReloadsAs(user, new { LoginCode = new { LastSentAt = clock.UtcNow.AddSeconds(-1) } });

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAfterCooldownEmailsAndStoresANewCode()
    {
        var user = Prepare(
            NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2))
        );

        var result = await ResendAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.LoginCode));
        email.Recipient.Should().Be(new EmailRecipient(user.Email!.Value, user.FirstName));
        emailSender.Sent.Should().ContainSingle();
        var code = email.Code;
        code.Should().NotBe("111111");
        user.LoginCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.LoginCode.LastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncWithoutPreviousCodeEmailsOne()
    {
        var user = Prepare(NewUser());

        var result = await ResendAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCode.Hash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictAndKeepsTheOldCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = Prepare(
            NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2))
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + "111111");
    }
}
