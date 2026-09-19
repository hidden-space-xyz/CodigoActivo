using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
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
    private readonly ResetTwoFactorCommandHandler sut;

    public ResetTwoFactorCommandHandlerTests()
    {
        actingAdmin = NewUser(isAdmin: true);
        actingAdmin.PasswordHash = hasher.Hash(ActingPassword);
        sut = new ResetTwoFactorCommandHandler(
            users,
            PasswordGuards.Create(hasher, uow, clock),
            clock,
            uow,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<ResetTwoFactorCommandHandler>.Instance
        );
    }

    private Task<Result> HandleAsync(Guid userId, string currentPassword)
    {
        return sut.HandleAsync(
            new ResetTwoFactorCommand(
                userId,
                actingAdmin.Id,
                new ResetTwoFactorRequest(currentPassword)
            ),
            TestContext.Current.CancellationToken
        );
    }

    private static User UserWithAuthenticator()
    {
        var user = NewUser();
        user.TwoFactorMethod = TwoFactorMethod.Authenticator;
        user.AuthenticatorKey = "protected:secret";
        user.AuthenticatorLastUsedStep = 12;
        user.PendingAuthenticatorKey = "protected:pending";
        user.LoginCodeHash = "code";
        user.TwoFactorFailedAttempts = 3;
        user.TwoFactorLockedUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
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

        var result = await HandleAsync(user.Id, "wrong");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
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

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(actingAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordReturnsUserToEmailAndClearsEverything()
    {
        var user = UserWithAuthenticator();
        users.FindReturns(actingAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.AuthenticatorLastUsedStep.Should().BeNull();
        user.PendingAuthenticatorKey.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message.TextBody.Should().NotContain("protected:secret").And.NotContain(ActingPassword);
    }
}
