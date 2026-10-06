using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly CommittedEvents events;
    private readonly ChangePasswordCommandHandler sut;

    public ChangePasswordCommandHandlerTests()
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
            ),
            new SessionsEndOnPasswordReplaced(sessions)
        );
        sut = new ChangePasswordCommandHandler(
            users,
            new ActingUserPolicy(currentUser, users),
            hasher,
            clock,
            PasswordGuards.Create(hasher, uow, clock)
        );
    }

    private Task AssertSessionsRevokedAsync(User user)
    {
        return sessions.Received(1).EndAllAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncPasswordNotSetReturnsBadRequest()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = default(string?) });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserPasswordNotSet);
    }

    [Fact]
    public async Task HandleAsyncNewPasswordSameAsCurrentReturnsBadRequestWithoutCountingAnAttempt()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("correct", "correct");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserNewPasswordSameAsCurrent);
        user.PasswordFailedAttempts.Should().Be(0);
        await events.PublishAsync(user);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncIncorrectCurrentPasswordReturnsBadRequest()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordHash.Should().Be(hasher.Hash("correct"));
        user.PasswordFailedAttempts.Should().Be(1, "the guard counts the failure on its own");
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordRehashesAndSignsOutEverywhere()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await events.PublishAsync(user);
        await AssertSessionsRevokedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordNotifiesTheOwnerWithoutSecrets()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordRequest("correct", "brandnew").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events.PublishAsync(user);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email!.Value);
        message
            .TextBody.Should()
            .NotContain("brandnew")
            .And.NotContain(user.PasswordHash)
            .And.NotContain("#userId=");
    }

    [Fact]
    public async Task HandleAsyncNotificationFailureStillChangesThePassword()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordRequest("correct", "brandnew").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        await events.PublishAsync(user);
        await AssertSessionsRevokedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncLeavesTheAlertAndTheSignOutToTheCommit()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        var result = await sut.HandleAsync(
            new ChangePasswordRequest("correct", "brandnew").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender
            .Sent.Should()
            .BeEmpty("the alert is only sent once the commit publishes the change");
        await sessions
            .DidNotReceiveWithAnyArgs()
            .EndAllAsync(default, TestContext.Current.CancellationToken);
        user.PullDomainEvents().Should().Equal(new PasswordChanged(user.Id));
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordClearsPendingPasswordReset()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        user.IssuePasswordResetCode(hasher.Hash("otp"), clock.UtcNow, TimeSpan.FromMinutes(15));
        users.FindReturns(user);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetExpiresAt.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
    }
}
