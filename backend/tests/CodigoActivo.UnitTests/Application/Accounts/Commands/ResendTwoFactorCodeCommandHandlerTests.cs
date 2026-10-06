using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
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

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

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
            new AccountEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test" },
                new TestClock()
            ),
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        sut = new ResendTwoFactorCodeCommandHandler(
            users,
            uow,
            clock,
            options,
            new LoginCodeIssuer(
                hasher,
                options,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private Task<Result> ResendAsync(Guid userId)
    {
        return sut.HandleAsync(
            new ResendTwoFactorCodeCommand(UserId.From(userId)),
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

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflict()
    {
        var user = users.FindReturns(NewUserWithAuthenticator("SECRET"));

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = users.FindReturns(NewUserWithLoginCode(clock));
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1) });

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = users.FindReturns(
            NewUserWithLoginCode(clock, lastSentAt: clock.UtcNow.AddSeconds(-30))
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAfterCooldownEmailsAndStoresANewCode()
    {
        var user = users.FindReturns(
            NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2))
        );

        var result = await ResendAsync(user.Id.Value);

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

        var result = await ResendAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictAndKeepsTheOldCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = users.FindReturns(
            NewUserWithLoginCode(clock, code: "111111", lastSentAt: clock.UtcNow.AddMinutes(-2))
        );

        var result = await ResendAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + "111111");
        await AssertNotSavedAsync();
    }
}
