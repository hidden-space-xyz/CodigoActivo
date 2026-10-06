using AwesomeAssertions;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class ResetTwoFactorCommandHandlerTests
{
    private const string ActingPassword = "acting-admin-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly User actingAdmin;
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly CommittedEvents events;
    private readonly ResetTwoFactorCommandHandler sut;

    public ResetTwoFactorCommandHandlerTests()
    {
        actingAdmin = NewUser(isAdmin: true);
        Persisted.Overwrite(actingAdmin, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = actingAdmin.Id;
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
        sut = new ResetTwoFactorCommandHandler(
            users,
            currentUser,
            PasswordGuards.Create(hasher, uow, clock),
            clock
        );
    }

    private Task<Result> HandleAsync(Guid userId, string currentPassword)
    {
        return sut.HandleAsync(
            new ResetTwoFactorRequest(currentPassword).ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private static User UserWithAuthenticator()
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorMethod = TwoFactorMethod.Authenticator,
                AuthenticatorKey = "protected:secret",
                AuthenticatorLastUsedStep = (long?)12,
                PendingAuthenticatorKey = "protected:pending",
                LoginCodeHash = "code",
                TwoFactorFailedAttempts = 3,
                TwoFactorLockedUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        return user;
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncWrongActingPasswordReturnsBadRequestWithoutLoadingTheTarget()
    {
        var user = UserWithAuthenticator();
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, "wrong");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        actingAdmin.PasswordFailedAttempts.Should().Be(1);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncActingUserMissingReturnsBadRequest()
    {
        users.FindReturns(null, UserWithAuthenticator());

        var result = await HandleAsync(Guid.NewGuid(), ActingPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(actingAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordReturnsUserToEmailAndClearsEverything()
    {
        var user = UserWithAuthenticator();
        users.FindReturns(actingAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id.Value, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.AuthenticatorLastUsedStep.Should().BeNull();
        user.PendingAuthenticatorKey.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await events.PublishAsync(user);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email!.Value);
        message.TextBody.Should().NotContain("protected:secret").And.NotContain(ActingPassword);
    }

    [Fact]
    public async Task HandleAsyncEmailUserOnlyClearsTheLockWithoutNotifying()
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new { TwoFactorFailedAttempts = 5, TwoFactorLockedUntil = clock.UtcNow.AddMinutes(10) }
        );
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        await events.PublishAsync(user);
        emailSender.Sent.Should().BeEmpty();
    }
}
