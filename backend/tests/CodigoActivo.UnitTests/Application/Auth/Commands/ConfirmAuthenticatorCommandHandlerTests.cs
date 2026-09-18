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

public sealed class ConfirmAuthenticatorCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ConfirmAuthenticatorCommandHandler sut;

    public ConfirmAuthenticatorCommandHandlerTests()
    {
        sut = new ConfirmAuthenticatorCommandHandler(
            users,
            uow,
            clock,
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<ConfirmAuthenticatorCommandHandler>.Instance
        );
    }

    private Task<Result> ConfirmAsync(Guid userId, string code)
    {
        return sut.HandleAsync(
            new ConfirmAuthenticatorCommand(userId, new ConfirmAuthenticatorRequest(code)),
            TestContext.Current.CancellationToken
        );
    }

    private User PendingUser(DateTimeOffset? expiresAt = null)
    {
        var user = NewUser();
        user.PendingAuthenticatorKey = FakeSecretProtector.Prefix + Secret;
        user.PendingAuthenticatorExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(10);
        user.LoginCodeHash = "stale";
        return users.FindReturns(user);
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

        var result = await ConfirmAsync(Guid.NewGuid(), "123456");

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncNoPendingKeyReturnsBadRequest()
    {
        var user = users.FindReturns(NewUser());

        var result = await ConfirmAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.AuthenticatorSetupExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncExpiredPendingKeyReturnsBadRequest()
    {
        var user = PendingUser(expiresAt: clock.UtcNow.AddSeconds(-1));

        var result = await ConfirmAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.AuthenticatorSetupExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestAndKeepsThePendingKey()
    {
        var user = PendingUser();
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns((long?)null);

        var result = await ConfirmAsync(user.Id, "000000");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.PendingAuthenticatorKey.Should().NotBeNull();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeActivatesAuthenticatorAndRemembersTheStep()
    {
        var user = PendingUser();
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(99);

        var result = await ConfirmAsync(user.Id, "123456");

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.AuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + Secret);
        user.AuthenticatorLastUsedStep.Should().Be(99);
        user.PendingAuthenticatorKey.Should().BeNull();
        user.PendingAuthenticatorExpiresAt.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message.TextBody.Should().NotContain(Secret).And.NotContain("123456");
    }
}
