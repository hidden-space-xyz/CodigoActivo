using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly CommittedEvents events;
    private readonly ResetPasswordCommandHandler sut;

    public ResetPasswordCommandHandlerTests()
    {
        events = new CommittedEvents(
            new AccountSecurityNotifications(
                users,
                new AccountSecurityNotifier(
                    emailSender,
                    clock,
                    composer,
                    NullLogger<AccountSecurityNotifier>.Instance
                ),
                NullLogger<AccountSecurityNotifications>.Instance
            ),
            new SessionsEndOnPasswordReplacedOrLocked(sessions)
        );
        sut = new ResetPasswordCommandHandler(
            users,
            AccountLocks.Create(users, Substitute.For<IUnitOfWork>()),
            clock,
            new FakePasswordHasher(),
            new OtpValidator(new FakeOneTimeCodeHasher())
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

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("some-code", "newPassword123").ToCommand(UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncNoCodeRequestedReturnsBadRequest()
    {
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("some-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task HandleAsyncExpiredCodeReturnsBadRequest()
    {
        var user = users.FindReturns(
            NewUserWithResetCode(clock, expiresAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("the-reset-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestWithoutPersisting()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-real-code"));
        var previousPasswordHash = user.PasswordHash;

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("a-wrong-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.PasswordResetInvalidOrExpired);
        user.PasswordHash.Should().Be(previousPasswordHash);
        user.PasswordResetCode.Hash.Should().NotBeNull("a wrong guess must not consume the code");
    }

    [Fact]
    public async Task HandleAsyncUserBlockedAfterRequestReturnsBadRequest()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));
        Persisted.Overwrite(user, new { Status = UserStatus.Blocked });

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("the-reset-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task HandleAsyncCodeUsedByAParallelResetBeforeTheLockReturnsBadRequest()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));
        users.ReloadsAs(
            user,
            new
            {
                PasswordHash = FakePasswordHasher.Prefix + "parallelReset1",
                PasswordResetCode = new OneTimeCode(null, null, null),
            }
        );

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("the-reset-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.PasswordResetInvalidOrExpired);
        user.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "parallelReset1");
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeChangesPasswordAndClearsCode()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("  THE-RESET-CODE  ", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );
        await events.PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "newPassword123");
        user.PasswordResetCode.Hash.Should().BeNull();
        user.PasswordResetCode.ExpiresAt.Should().BeNull();
        user.PasswordResetCode.LastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await AssertSessionsRevokedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeQueuesExactlyOnePasswordResetAlertToTheOwner()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("the-reset-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );
        await events.PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.SecurityAlert),
                    new EmailRecipient(user.Email!.Value, user.FirstName)
                )
                {
                    Change = AccountSecurityChange.PasswordReset,
                    OccurredAt = clock.UtcNow,
                }
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncLeavesTheAlertAndTheSignOutToTheCommit()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));

        var result = await sut.HandleAsync(
            new ResetPasswordRequest("the-reset-code", "newPassword123").ToCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender
            .Sent.Should()
            .BeEmpty("the alert is only sent once the commit publishes the change");
        await sessions
            .DidNotReceiveWithAnyArgs()
            .EndAllAsync(default, TestContext.Current.CancellationToken);
        user.PullDomainEvents().Should().Equal(new PasswordReset(user.Id));
    }
}
