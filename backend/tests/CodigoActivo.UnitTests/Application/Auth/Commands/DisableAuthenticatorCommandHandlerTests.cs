using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class DisableAuthenticatorCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly RecordingEmailSender emailSender = new();
    private readonly DisableAuthenticatorCommandHandler sut;

    public DisableAuthenticatorCommandHandlerTests()
    {
        sut = new DisableAuthenticatorCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            options,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<DisableAuthenticatorCommandHandler>.Instance
        );
    }

    private Task<Result> DisableAsync(Guid userId, string password = "password123", string code = "123456")
    {
        return sut.HandleAsync(
            new DisableAuthenticatorCommand(userId, new DisableAuthenticatorRequest(password, code)),
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

        var result = await DisableAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailUserReturnsConflict()
    {
        var user = users.FindReturns(NewUser());

        var result = await DisableAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.AuthenticatorNotEnabled);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestBeforeCheckingTheCode()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));

        var result = await DisableAsync(user.Id, password: "wrong");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        totp.DidNotReceiveWithAnyArgs().MatchStep(default!, default!, default);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1);

        var result = await DisableAsync(user.Id);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongCodeCountsFailureAndKeepsTheAuthenticator()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns((long?)null);

        var result = await DisableAsync(user.Id);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.AuthenticatorKey.Should().NotBeNull();
        user.TwoFactorFailedAttempts.Should().Be(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactor()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        user.TwoFactorFailedAttempts = 1;
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns((long?)null);

        await DisableAsync(user.Id);

        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
    }

    [Fact]
    public async Task HandleAsyncReplayedCodeIsRejected()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret, lastUsedStep: 50));
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(50);

        var result = await DisableAsync(user.Id);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
    }

    [Fact]
    public async Task HandleAsyncPasswordAndCodeCorrectReturnsToEmailAndForgetsTheKey()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret, lastUsedStep: 50));
        user.TwoFactorFailedAttempts = 1;
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(51);

        var result = await DisableAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.AuthenticatorLastUsedStep.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message.TextBody.Should().NotContain(Secret).And.NotContain("123456");
    }
}
