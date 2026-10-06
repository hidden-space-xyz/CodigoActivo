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
using TwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ConfirmAuthenticatorCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly CommittedEvents events;
    private readonly ConfirmAuthenticatorCommandHandler sut;

    public ConfirmAuthenticatorCommandHandlerTests()
    {
        events = new CommittedEvents(
            new AccountSecurityNotifications(
                users,
                new AccountSecurityNotifier(
                    emailSender,
                    clock,
                    new AccountEmailComposer(new ApplicationOptions(), clock),
                    NullLogger<AccountSecurityNotifier>.Instance
                ),
                NullLogger<AccountSecurityNotifications>.Instance
            )
        );
        sut = new ConfirmAuthenticatorCommandHandler(
            users,
            currentUser,
            clock,
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            )
        );
    }

    private Task<Result> ConfirmAsync(Guid userId, string code)
    {
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new ConfirmAuthenticatorRequest(code).ToCommand(),
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

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await ConfirmAsync(Guid.NewGuid(), "123456");

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncNoPendingKeyReturnsBadRequest()
    {
        var user = users.FindReturns(NewUser());

        var result = await ConfirmAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.AuthenticatorSetupExpired);
    }

    [Fact]
    public async Task HandleAsyncExpiredPendingKeyReturnsBadRequest()
    {
        var user = PendingUser(expiresAt: clock.UtcNow.AddSeconds(-1));

        var result = await ConfirmAsync(user.Id.Value, "123456");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.AuthenticatorSetupExpired);
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestAndKeepsThePendingKey()
    {
        var user = PendingUser();
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns(default(long?));

        var result = await ConfirmAsync(user.Id.Value, "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.PendingAuthenticatorKey.Should().NotBeNull();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
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

        var result = await ConfirmAsync(user.Id.Value, "123456");
        await events.PublishAsync(user);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.AuthenticatorAlreadyEnabled);
        user.AuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + "ACTIVEKEY");
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeActivatesAuthenticatorAndRemembersTheStep()
    {
        var user = PendingUser();
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(99);

        var result = await ConfirmAsync(user.Id.Value, "123456");
        await events.PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.AuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + Secret);
        user.AuthenticatorLastUsedStep.Should().Be(99);
        user.PendingAuthenticatorKey.Should().BeNull();
        user.PendingAuthenticatorExpiresAt.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email!.Value);
        message.TextBody.Should().NotContain(Secret).And.NotContain("123456");
    }
}
