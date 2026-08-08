using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly PasswordResetOptions passwordReset = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly ForgotPasswordCommandHandler sut;

    public ForgotPasswordCommandHandlerTests()
    {
        sut = new ForgotPasswordCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            passwordReset,
            new AccountEmails(emailSender, verification, passwordReset, application),
            NullLogger<ForgotPasswordCommandHandler>.Instance
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_QuotaDenied_StillReportsSuccessAndIssuesNoCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Global);
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest(user.Email!)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsSuccessWithoutSending()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("nobody@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_UserWithoutPassword_ReturnsSuccessWithoutSending()
    {
        users.FindReturns(NewUser(passwordHash: null));

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid> IneligibleResetStatuses()
    {
        return [SeedIds.UserStatusTypes.Blocked, SeedIds.UserStatusTypes.Dependent];
    }

    [Theory]
    [MemberData(nameof(IneligibleResetStatuses))]
    public async Task HandleAsync_IneligibleStatus_ReturnsSuccessWithoutSending(Guid statusId)
    {
        users.FindReturns(NewUser(statusId: statusId));

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_WithinCooldown_ReturnsSuccessWithoutSending()
    {
        var user = users.FindReturns(NewUser());
        user.PasswordResetLastSentAt = clock.UtcNow.AddSeconds(-10);

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_EligibleUser_SendsGuidCodeAndPersistsHash()
    {
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var code = emailSender.LastCode();
        Guid.TryParse(code, out _).Should().BeTrue("the reset code is a GUID");
        user.PasswordResetCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.PasswordResetExpiresAt.Should().Be(clock.UtcNow + passwordReset.CodeLifetime);
        user.PasswordResetLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        emailSender.Sent[0].ToAddress.Should().Be(user.Email);
        emailSender.Sent[0].TextBody.Should().Contain("/reset-password?userId=");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CooldownElapsed_ReplacesPreviousCode()
    {
        var user = users.FindReturns(
            NewUserWithResetCode(clock, code: "old-code", lastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var newCode = emailSender.LastCode();
        newCode.Should().NotBe("old-code");
        user.PasswordResetCodeHash.Should().Be(FakePasswordHasher.Prefix + newCode);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmailSendFails_ReturnsSuccessWithoutPersisting()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(new ForgotPasswordRequest("ana@test.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }
}
