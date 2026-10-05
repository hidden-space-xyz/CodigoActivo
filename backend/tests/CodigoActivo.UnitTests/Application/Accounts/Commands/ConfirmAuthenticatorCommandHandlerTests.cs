using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

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
                new AccountEmailComposer(new ApplicationOptions(), clock),
                NullLogger<AccountSecurityNotifier>.Instance
            )
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
        Persisted.Overwrite(
            user,
            new
            {
                PendingAuthenticatorKey = FakeSecretProtector.Prefix + Secret,
                PendingAuthenticatorExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(10),
                LoginCodeHash = "stale",
            }
        );
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

        result.ShouldFail(ErrorKind.Validation, ErrorCode.AuthenticatorSetupExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncExpiredPendingKeyReturnsBadRequest()
    {
        var user = PendingUser(expiresAt: clock.UtcNow.AddSeconds(-1));

        var result = await ConfirmAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.AuthenticatorSetupExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestAndKeepsThePendingKey()
    {
        var user = PendingUser();
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns(default(long?));

        var result = await ConfirmAsync(user.Id, "000000");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.TwoFactorCodeInvalid);
        user.PendingAuthenticatorKey.Should().NotBeNull();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPendingKeyNeverReplacesAnActiveAuthenticator()
    {
        var user = PendingUser();
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorMethod = TwoFactorMethod.Authenticator,
                AuthenticatorKey = FakeSecretProtector.Prefix + "ACTIVEKEY",
            }
        );
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(99);

        var result = await ConfirmAsync(user.Id, "123456");

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.AuthenticatorAlreadyEnabled);
        user.AuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + "ACTIVEKEY");
        emailSender.Sent.Should().BeEmpty();
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
