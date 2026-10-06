using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
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
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakePasswordHasher hasher = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly RecordingLogger<ResendVerificationCommandHandler> logger = new();
    private readonly ResendVerificationCommandHandler sut;

    public ResendVerificationCommandHandlerTests()
    {
        sut = new ResendVerificationCommandHandler(
            users,
            uow,
            clock,
            hasher,
            verification,
            new AccountEmails(
                emailSender,
                new AccountEmailComposer(application, new TestClock()),
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

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAddressWithoutAccountSucceedsAfterTheSameHashing()
    {
        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        hasher.Hashes.Should().Be(1, "every request hashes a code, so timing tells nothing");
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncVerifiedAccountGetsNothing()
    {
        HolderOfTheAddress(NewUser(statusId: KnownIds.UserStatusTypes.Active));

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
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
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + emailSender.LastCode());
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownSendsNothing()
    {
        HolderOfTheAddress(NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddSeconds(-10)));

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCooldownElapsedIssuesNewCodeAndPersists()
    {
        var user = HolderOfTheAddress(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        var newCode = emailSender.LastCode();
        newCode
            .Should()
            .MatchRegex("^[0-9a-f]{64}$", "the OTP is 256 random bits in lowercase hex");
        newCode.Should().NotBe("old-code");
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + newCode);
        user.OtpExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEmailSendFailsLogsAndKeepsTheIssuedCode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("the outbox insert failed");
        var user = HolderOfTheAddress(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        user.OtpCodeHash.Should().Be(previousHash);
        await AssertNotSavedAsync();
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
        var previousHash = user.OtpCodeHash;
        var previousSentAt = user.OtpLastSentAt;

        var result = await HandleAsync();

        result.IsSuccess.Should().BeTrue();
        user.OtpCodeHash.Should().Be(previousHash);
        user.OtpLastSentAt.Should().Be(previousSentAt);
        await AssertNotSavedAsync();
    }
}
