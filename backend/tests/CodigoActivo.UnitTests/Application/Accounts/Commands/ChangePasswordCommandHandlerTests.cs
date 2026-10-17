using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
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
    private readonly FakeAccountEmailComposer composer = new();
    private readonly TestCurrentUser currentUser = new();
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
                    composer,
                    NullLogger<AccountSecurityNotifier>.Instance
                )
            ),
            new SessionsEndOnPasswordReplacedOrLocked(sessions)
        );
        sut = new ChangePasswordCommandHandler(
            users,
            AccountLocks.Create(users, uow),
            currentUser,
            hasher,
            clock,
            PasswordGuards.Create(hasher, uow, clock)
        );
    }

    private UserId Self => currentUser.Id!.Value;

    private Task AssertSessionsRevokedAsync(User user)
    {
        return sessions.Received(1).EndAllAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsyncAnotherAccountReturnsForbiddenWithoutCountingAnAttempt(
        bool isAdmin
    )
    {
        currentUser.IsAdmin = isAdmin;
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.HandleAsync(
            request.ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        user.PasswordHash.Should().Be(hasher.Hash("correct"));
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            request.ToCommand(Self),
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
            request.ToCommand(Self),
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
            request.ToCommand(Self),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserNewPasswordSameAsCurrent);
        user.PasswordLockout.FailedAttempts.Should().Be(0);
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
            request.ToCommand(Self),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordHash.Should().Be(hasher.Hash("correct"));
        user.PasswordLockout.FailedAttempts.Should()
            .Be(1, "the guard counts the failure on its own");
    }

    [Fact]
    public async Task HandleAsyncPasswordReplacedBeforeTheLockReturnsCurrentPasswordIncorrect()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        users.ReloadsAs(user, new { PasswordHash = hasher.Hash("reset-meanwhile") });
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.HandleAsync(
            request.ToCommand(Self),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordHash.Should().Be(hasher.Hash("reset-meanwhile"));
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
            request.ToCommand(Self),
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
            new ChangePasswordRequest("correct", "brandnew").ToCommand(Self),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events.PublishAsync(user);
        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.SecurityAlert),
                    new EmailRecipient(user.Email!.Value, user.FirstName)
                )
                {
                    Change = AccountSecurityChange.PasswordChanged,
                    OccurredAt = clock.UtcNow,
                }
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncNotificationFailureStillChangesThePassword()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordRequest("correct", "brandnew").ToCommand(Self),
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
            new ChangePasswordRequest("correct", "brandnew").ToCommand(Self),
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
            request.ToCommand(Self),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCode.Hash.Should().BeNull();
        user.PasswordResetCode.ExpiresAt.Should().BeNull();
        user.PasswordResetCode.LastSentAt.Should().BeNull();
    }
}
