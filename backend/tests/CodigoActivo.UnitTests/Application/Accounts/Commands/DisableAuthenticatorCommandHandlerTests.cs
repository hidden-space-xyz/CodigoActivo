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

public sealed class DisableAuthenticatorCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly CommittedEvents events;
    private readonly DisableAuthenticatorCommandHandler sut;

    public DisableAuthenticatorCommandHandlerTests()
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
        sut = new DisableAuthenticatorCommandHandler(
            users,
            currentUser,
            uow,
            clock,
            PasswordGuards.Create(new FakePasswordHasher(), uow, clock),
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            options,
            NullLogger<DisableAuthenticatorCommandHandler>.Instance
        );
    }

    private Task<Result> DisableAsync(
        Guid userId,
        string password = "password123",
        string code = "123456"
    )
    {
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new DisableAuthenticatorRequest(password, code).ToCommand(),
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

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailUserReturnsConflict()
    {
        var user = users.FindReturns(NewUser());

        var result = await DisableAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.AuthenticatorNotEnabled);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestBeforeCheckingTheCode()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));

        var result = await DisableAsync(user.Id.Value, password: "wrong");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        totp.DidNotReceiveWithAnyArgs().MatchStep(default!, default!, default);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.PasswordFailedAttempts.Should().Be(1);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1) });

        var result = await DisableAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongCodeCountsFailureAndKeepsTheAuthenticator()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(default(long?));

        var result = await DisableAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.AuthenticatorKey.Should().NotBeNull();
        user.TwoFactorFailedAttempts.Should().Be(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactor()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 1 });
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(default(long?));

        await DisableAsync(user.Id.Value);

        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
    }

    [Fact]
    public async Task HandleAsyncStepAcceptedByAParallelRequestIsRejected()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret, lastUsedStep: 50));
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(51);
        users
            .LockAsync(user, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Persisted.Overwrite(user, new { AuthenticatorLastUsedStep = 51L });
                return true;
            });

        var result = await DisableAsync(user.Id.Value);
        await events.PublishAsync(user);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorRemovedByAParallelRequestReturnsConflict()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));
        users
            .LockAsync(user, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                user.UseEmailTwoFactor(clock.UtcNow);
                return true;
            });

        var result = await DisableAsync(user.Id.Value);
        await events.PublishAsync(user);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.AuthenticatorNotEnabled);
        totp.DidNotReceiveWithAnyArgs().MatchStep(default!, default!, default);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncReplayedCodeIsRejected()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret, lastUsedStep: 50));
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(50);

        var result = await DisableAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
    }

    [Fact]
    public async Task HandleAsyncPasswordAndCodeCorrectReturnsToEmailAndForgetsTheKey()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret, lastUsedStep: 50));
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 1 });
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(51);

        var result = await DisableAsync(user.Id.Value);
        await events.PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.AuthenticatorLastUsedStep.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email!.Value);
        message.TextBody.Should().NotContain(Secret).And.NotContain("123456");
    }
}
