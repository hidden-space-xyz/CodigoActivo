using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class ResendVerificationCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly PasswordResetOptions passwordReset = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly ResendVerificationCommandHandler sut;

    public ResendVerificationCommandHandlerTests()
    {
        sut = new ResendVerificationCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            verification,
            new AccountEmails(emailSender, verification, passwordReset, application)
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UserNotPending_ReturnsConflict()
    {
        var user = users.FindReturns(NewUser(statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PendingUserWithoutEmail_ReturnsConflict()
    {
        var user = users.FindReturns(NewPendingWithOtp(clock));
        user.Email = null;

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NeverSentBefore_AllowsImmediateResend()
    {
        var user = users.FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Pending,
                otpCodeHash: null,
                otpExpiresAt: null,
                otpLastSentAt: null
            )
        );

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + emailSender.LastCode());
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_VerificationNotRequired_ReturnsConflict()
    {
        verification.Required = false;
        var user = users.FindReturns(NewPendingWithOtp(clock));

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithinCooldown_ReturnsConflict()
    {
        var user = users.FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddSeconds(-10))
        );

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_CooldownElapsed_IssuesNewCodeAndPersists()
    {
        var user = users.FindReturns(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var newCode = emailSender.LastCode();
        Guid.TryParse(newCode, out _).Should().BeTrue("the OTP is now a GUID");
        newCode.Should().NotBe("old-code");
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + newCode);
        user.OtpExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmailSendFails_DoesNotPersistNewCode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = users.FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;

        var act = () =>
            sut.HandleAsync(
                new ResendVerificationCommand(user.Id),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        user.OtpCodeHash.Should().Be(previousHash);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_QuotaDenied_ReturnsConflictAndKeepsTheIssuedCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = users.FindReturns(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;
        var previousSentAt = user.OtpLastSentAt;

        var result = await sut.HandleAsync(
            new ResendVerificationCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendCooldownActive);
        user.OtpCodeHash.Should().Be(previousHash);
        user.OtpLastSentAt.Should().Be(previousSentAt);
        await AssertNotSavedAsync();
    }
}
