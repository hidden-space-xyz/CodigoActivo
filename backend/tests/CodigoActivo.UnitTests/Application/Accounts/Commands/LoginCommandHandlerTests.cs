using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;
using TwoFactorMethod = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

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
            new AccountEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test" },
                new TestClock()
            ),
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            twoFactor
        );
        sut = new LoginCommandHandler(
            users,
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock, emailSender: emailSender),
            twoFactor,
            new LoginCodeIssuer(
                new FakeOneTimeCodeHasher(),
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
            new LoginCommand(identifier, password),
            TestContext.Current.CancellationToken
        );
    }

    private User Returns(User user)
    {
        users.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>()).Returns(user);
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
            .GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await LoginAsync("nobody@test.com");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUnknownIdentifierStillPaysTheSameHashingWorkAsAKnownOne()
    {
        User? missing = null;
        users
            .GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await LoginAsync("nobody@test.com");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
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

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordDoesNotVerifyReturnsUnauthorizedWithoutEmailing()
    {
        var user = Returns(NewUser(passwordHash: "fake:correct"));

        var result = await LoginAsync(password: "wrong");

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
        emailSender.Sent.Should().BeEmpty();
        user.PasswordFailedAttempts.Should().Be(1, "the guard counts the failure on its own");
        user.PasswordLockedAt.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Theory]
    [MemberData(nameof(BlockedStatuses))]
    public async Task HandleAsyncNonActiveStatusReturnsForbidden(
        Guid statusId,
        ApplicationErrorCode expected
    )
    {
        Returns(NewUser(statusId: statusId));

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Forbidden, expected);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid, ApplicationErrorCode> BlockedStatuses()
    {
        return new()
        {
            { KnownIds.UserStatusTypes.Blocked, ApplicationErrorCode.UserAccountBlocked },
            { KnownIds.UserStatusTypes.Dependent, ApplicationErrorCode.UserAccountIsDependent },
            {
                KnownIds.UserStatusTypes.Pending,
                ApplicationErrorCode.UserAccountPendingVerification
            },
        };
    }

    [Fact]
    public async Task HandleAsyncValidCredentialsEmailsCodeStoresHashAndDoesNotRecordLogin()
    {
        var user = Returns(NewUser());

        var result = await LoginAsync("  Ana@Test.com  ");

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LoginChallenge(user.Id.Value, TwoFactorMethod.Email, "a***@test.com"));
        result
            .Value.ToResponse()
            .Should()
            .Be(new LoginChallengeResponse(TwoFactorMethod.Email, "a***@test.com"));

        var code = emailSender.LastLoginCode();
        emailSender.Sent.Should().ContainSingle().Which.Kind.Should().Be(EmailKind.TwoFactorCode);
        user.LoginCodeHash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow + twoFactor.ChallengeLifetime);
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
        user.LastLoginAt.Should().BeNull("the login only completes after the second factor");
        await users
            .Received(1)
            .GetByEmailAsync(EmailAddress.FromStored("ana@test.com"), Arg.Any<CancellationToken>());
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
        user.LoginCodeHash.Should().Be(FakeOneTimeCodeHasher.Prefix + "654321");
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
        user.LoginCodeHash.Should().NotBe(FakeOneTimeCodeHasher.Prefix + "654321");
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserOpensChallengeWithoutEmailingOrMaskedAddress()
    {
        var user = Returns(NewUserWithAuthenticator("SECRET"));

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LoginChallenge(user.Id.Value, TwoFactorMethod.Authenticator, null));
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLockedAccountAnswersLikeAWrongPasswordAndStillHashes()
    {
        var user = Returns(NewUser(passwordHash: "fake:password123"));
        Persisted.Overwrite(user, new { PasswordLockedAt = clock.UtcNow.AddMinutes(-5) });
        var before = hasher.VerifyCalls;

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
        hasher.VerifyCalls.Should().Be(before + 1);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedAccountWithAWrongPasswordAnswersTheSameWay()
    {
        var user = Returns(NewUser(passwordHash: "fake:password123"));
        Persisted.Overwrite(user, new { PasswordLockedAt = clock.UtcNow.AddMinutes(-5) });

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

        result.ShouldFail(ErrorKind.Unauthorized, ApplicationErrorCode.InvalidCredentials);
        user.LoginChallengeId.Should().Be(challengeId);
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordForgetsEarlierWrongAttempts()
    {
        var user = Returns(NewUser());
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 3 });

        var result = await LoginAsync();

        result.IsSuccess.Should().BeTrue();
        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncSecondFactorLockedReturnsForbiddenWithoutEmailing()
    {
        var user = Returns(NewUser());
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(5) });

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockExpiredOpensChallengeAgain()
    {
        var user = Returns(NewUser());
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(-1) });

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

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailTransportFailsReturnsConflictWithoutStoringACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = Returns(NewUser());

        var result = await LoginAsync();

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailUserWithoutAddressReturnsConflict()
    {
        Returns(NewUser(email: null));

        var result = await LoginAsync("nobody@test.com");

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
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
