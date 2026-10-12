using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ResendVerificationCommandHandlerTests
{
    private const string Email = "ana@test.com";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly RecordingLogger<ResendVerificationCommandHandler> logger = new();
    private readonly ResendVerificationCommandHandler sut;

    public ResendVerificationCommandHandlerTests()
    {
        sut = new ResendVerificationCommandHandler(
            users,
            clock,
            new FakeOneTimeCodeHasher(),
            verification,
            new AccountEmails(
                emailSender,
                composer,
                verification,
                new PasswordResetOptions(),
                new TwoFactorOptions()
            ),
            logger
        );
    }

    private Task<Result> HandleAsync(string email = $"  {Email}  ")
    {
        return sut.HandleAsync(
            new ResendVerificationCommand(email),
            TestContext.Current.CancellationToken
        );
    }

    private User HolderOfTheAddress(User user)
    {
        users
            .GetByEmailAsync(EmailAddress.FromStored(Email), Arg.Any<CancellationToken>())
            .Returns(user);
        return user;
    }

    [Fact]
    public async Task HandleAsyncAddressWithoutAccountSucceedsWithoutSending()
    {
        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncVerifiedAccountGetsNothing()
    {
        HolderOfTheAddress(NewUser(statusId: KnownIds.UserStatusTypes.Active));

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNeverSentBeforeAllowsImmediateResend()
    {
        var user = HolderOfTheAddress(
            NewUser(
                statusId: KnownIds.UserStatusTypes.Pending,
                otpCodeHash: null,
                otpExpiresAt: null,
                otpLastSentAt: null
            )
        );

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.AccountVerification));
        email.Recipient.Should().Be(new EmailRecipient(Email, string.Empty));
        email.UserId.Should().Be(user.Id);
        user.VerificationCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + email.Code);
        user.VerificationCode.LastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownSendsNothing()
    {
        HolderOfTheAddress(NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddSeconds(-10)));

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncCooldownElapsedIssuesNewCode()
    {
        var user = HolderOfTheAddress(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.AccountVerification));
        email.Lifetime.Should().Be(verification.OtpLifetime);
        var newCode = email.Code;
        newCode
            .Should()
            .MatchRegex("^[0-9a-f]{64}$", "the OTP is 256 random bits in lowercase hex");
        newCode.Should().NotBe("old-code");
        user.VerificationCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + newCode);
        user.VerificationCode.ExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        user.VerificationCode.LastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsyncEmailSendFailsLogsAndKeepsTheIssuedCode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("the outbox insert failed");
        var user = HolderOfTheAddress(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.VerificationCode.Hash;

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        user.VerificationCode.Hash.Should().Be(previousHash);
        logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Error)
            .Which.Message.Should()
            .StartWith("Sending a AccountVerification email failed")
            .And.NotContain(user.Id.ToString());
        logger.Entries.Should().NotContain(entry => entry.Contains(user.Email!.Value));
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedKeepsTheIssuedCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = HolderOfTheAddress(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.VerificationCode.Hash;
        var previousSentAt = user.VerificationCode.LastSentAt;

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        user.VerificationCode.Hash.Should().Be(previousHash);
        user.VerificationCode.LastSentAt.Should().Be(previousSentAt);
    }
}
