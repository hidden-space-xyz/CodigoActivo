using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class LoginCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly TwoFactorOptions twoFactor = new();
    private readonly CountingPasswordHasher hasher = new();
    private readonly LoginCommandHandler sut;

    public LoginCommandHandlerTests()
    {
        var accountEmails = new AccountEmails(
            emailSender,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            new ApplicationOptions { BaseUrl = "https://app.test" },
            twoFactor
        );
        sut = new LoginCommandHandler(
            users,
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock, emailSender: emailSender),
            twoFactor,
            new LoginCodeIssuer(
                hasher,
                twoFactor,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private Task<Result<LoginChallenge>> LoginAsync(
        string identifier = "ana@test.com",
        string password = "password123"
    )
    {
        return sut.HandleAsync(
            new LoginCommand(new LoginRequest(identifier, password)),
            TestContext.Current.CancellationToken
        );
    }

    private User Returns(User user)
    {
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserNotFoundReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await LoginAsync("nobody@test.com");

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUnknownIdentifierStillPaysTheSameHashingWorkAsAKnownOne()
    {
        User? missing = null;
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await LoginAsync("nobody@test.com");

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        hasher.VerifyCalls.Should().Be(1);

        Returns(NewUser(passwordHash: "fake:correct"));
        await LoginAsync(password: "wrong");

        hasher.VerifyCalls.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task HandleAsyncPasswordHashNotSetReturnsUnauthorized(string? hash)
    {
        Returns(NewUser(passwordHash: hash));

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordDoesNotVerifyReturnsUnauthorizedWithoutEmailing()
    {
        var user = Returns(NewUser(passwordHash: "fake:correct"));

        var result = await LoginAsync(password: "wrong");

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        emailSender.Sent.Should().BeEmpty();
        user.PasswordFailedAttempts.Should().Be(1, "the guard counts the failure on its own");
        user.PasswordLockedAt.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Theory]
    [MemberData(nameof(BlockedStatuses))]
    public async Task HandleAsyncNonActiveStatusReturnsForbidden(Guid statusId, ErrorCode expected)
    {
        Returns(NewUser(statusId: statusId));

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Forbidden, expected);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid, ErrorCode> BlockedStatuses()
    {
        return new()
        {
            { SeedIds.UserStatusTypes.Blocked, ErrorCode.UserAccountBlocked },
            { SeedIds.UserStatusTypes.Dependent, ErrorCode.UserAccountIsDependent },
            { SeedIds.UserStatusTypes.Pending, ErrorCode.UserAccountPendingVerification },
        };
    }

    [Fact]
    public async Task HandleAsyncValidCredentialsEmailsCodeStoresHashAndDoesNotRecordLogin()
    {
        var user = Returns(NewUser());

        var result = await LoginAsync("  ana@test.com  ");

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LoginChallenge(user.Id, TwoFactorMethod.Email, "a***@test.com"));
        result
            .Value.ToResponse()
            .Should()
            .Be(new LoginChallengeResponse(TwoFactorMethod.Email, "a***@test.com"));

        var code = emailSender.LastLoginCode();
        emailSender.Sent.Should().ContainSingle().Which.Kind.Should().Be(EmailKind.TwoFactorCode);
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow + twoFactor.ChallengeLifetime);
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
        user.LastLoginAt.Should().BeNull("the login only completes after the second factor");
        await users
            .Received(1)
            .GetByEmailOrPhoneAsync("ana@test.com", Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRecentCodeStillValidReusesItInsteadOfEmailingAgain()
    {
        var user = Returns(
            NewUserWithLoginCode(clock, code: "654321", lastSentAt: clock.UtcNow.AddSeconds(-30))
        );

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + "654321");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncOldCodeOutsideCooldownIssuesANewOne()
    {
        var user = Returns(
            NewUserWithLoginCode(clock, code: "654321", lastSentAt: clock.UtcNow.AddMinutes(-2))
        );

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBe(FakePasswordHasher.Prefix + "654321");
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserOpensChallengeWithoutEmailingOrMaskedAddress()
    {
        var user = Returns(NewUserWithAuthenticator("SECRET"));

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new LoginChallenge(user.Id, TwoFactorMethod.Authenticator, null));
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLockedAccountAnswersLikeAWrongPasswordAndStillHashes()
    {
        var user = Returns(NewUser(passwordHash: "fake:password123"));
        user.PasswordLockedAt = clock.UtcNow.AddMinutes(-5);
        var before = hasher.VerifyCalls;

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        hasher.VerifyCalls.Should().Be(before + 1);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedAccountWithAWrongPasswordAnswersTheSameWay()
    {
        var user = Returns(NewUser(passwordHash: "fake:password123"));
        user.PasswordLockedAt = clock.UtcNow.AddMinutes(-5);

        var correct = await LoginAsync();
        var wrong = await LoginAsync(password: "not-the-password");

        correct.Error.Should().BeEquivalentTo(wrong.Error);
        user.PasswordFailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFifthWrongPasswordLocksTheAccountAndEmailsItsOwner()
    {
        var user = Returns(NewUser(passwordHash: "fake:password123"));

        for (var attempt = 0; attempt < PasswordLockoutOptions.DefaultMaxFailedAttempts; attempt++)
        {
            await LoginAsync(password: "not-the-password");
        }

        user.IsPasswordLocked().Should().BeTrue();
        user.PasswordLockedAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().ContainSingle().Which.Kind.Should().Be(EmailKind.SecurityAlert);
    }

    [Fact]
    public async Task HandleAsyncAcceptedPasswordStoresAFreshChallengeIdentifier()
    {
        var user = Returns(NewUser());

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        user.LoginChallengeId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HandleAsyncSecondPasswordStepRotatesTheChallengeIdentifier()
    {
        var user = Returns(NewUser());
        await LoginAsync();
        var first = user.LoginChallengeId;

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        await LoginAsync();

        first.Should().NotBeNull();
        user.LoginChallengeId.Should().NotBeNull();
        user.LoginChallengeId.Should().NotBe(first!.Value);
    }

    [Fact]
    public async Task HandleAsyncRefusedPasswordLeavesTheOpenChallengeUntouched()
    {
        var user = Returns(NewUser(passwordHash: "fake:correct"));
        var challengeId = Guid.NewGuid();
        user.StartLoginChallenge(challengeId);

        var result = await LoginAsync(password: "not-the-password");

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        user.LoginChallengeId.Should().Be(challengeId);
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordForgetsEarlierWrongAttempts()
    {
        var user = Returns(NewUser());
        user.PasswordFailedAttempts = 3;

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncSecondFactorLockedReturnsForbiddenWithoutEmailing()
    {
        var user = Returns(NewUser());
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(5);

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockExpiredOpensChallengeAgain()
    {
        var user = Returns(NewUser());
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(-1);

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsyncEmailQuotaDeniedReturnsConflictWithoutStoringACode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = Returns(NewUser());

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailTransportFailsReturnsConflictWithoutStoringACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = Returns(NewUser());

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailUserWithoutAddressReturnsConflict()
    {
        Returns(NewUser(email: null));

        var result = await LoginAsync("+34123456789");

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserContactInfoRequired);
        await AssertNotSavedAsync();
    }

    private sealed class CountingPasswordHasher : IPasswordHasher
    {
        private readonly FakePasswordHasher inner = new();

        public int VerifyCalls { get; private set; }

        public string Hash(string password)
        {
            return inner.Hash(password);
        }

        public bool Verify(string password, string hash)
        {
            VerifyCalls++;
            return inner.Verify(password, hash);
        }
    }
}
