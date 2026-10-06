using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
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

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly RecordingEmailSender emailSender = new();
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
                    new AccountEmailComposer(new ApplicationOptions(), clock),
                    NullLogger<AccountSecurityNotifier>.Instance
                ),
                NullLogger<AccountSecurityNotifications>.Instance
            ),
            new SessionsEndOnPasswordReplaced(sessions)
        );
        sut = new ResetPasswordCommandHandler(
            users,
            clock,
            new FakePasswordHasher(),
            new OtpValidator(new FakePasswordHasher())
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
        user.PasswordResetCodeHash.Should().NotBeNull("a wrong guess must not consume the code");
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
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetExpiresAt.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
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
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email!.Value);
        message.TextBody.Should().NotContain("newPassword123").And.NotContain("the-reset-code");
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
