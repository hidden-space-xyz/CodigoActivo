using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class ResendTwoFactorCodeCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly TwoFactorOptions options = new();
    private readonly ResendTwoFactorCodeCommandHandler sut;

    public ResendTwoFactorCodeCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        var accountEmails = new AccountEmails(
            emailSender,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            new ApplicationOptions { BaseUrl = "https://app.test" },
            options
        );
        sut = new ResendTwoFactorCodeCommandHandler(
            users,
            uow,
            clock,
            options,
            new LoginCodeIssuer(hasher, options, accountEmails, NullLogger<LoginCodeIssuer>.Instance)
        );
    }

    private Task<Result> ResendAsync(Guid userId)
    {
        return sut.HandleAsync(
            new ResendTwoFactorCodeCommand(userId),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await ResendAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflict()
    {
        var user = users.FindReturns(NewUserWithAuthenticator("SECRET"));

        var result = await ResendAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = users.FindReturns(NewUserWithLoginCode(clock));
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1);

        var result = await ResendAsync(user.Id);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = users.FindReturns(NewUserWithLoginCode(clock, lastSentAt: clock.UtcNow.AddSeconds(-30)));

        var result = await ResendAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAfterCooldownEmailsAndStoresANewCode()
    {
        var user = users.FindReturns(NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2)));

        var result = await ResendAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        var code = emailSender.LastLoginCode();
        code.Should().NotBe("111111");
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncWithoutPreviousCodeEmailsOne()
    {
        var user = users.FindReturns(NewUser());

        var result = await ResendAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictAndKeepsTheOldCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = users.FindReturns(NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2)));

        var result = await ResendAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + "111111");
        await AssertNotSavedAsync();
    }
}
