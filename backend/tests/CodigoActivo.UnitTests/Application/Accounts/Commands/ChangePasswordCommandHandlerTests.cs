using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
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
    private readonly ChangePasswordCommandHandler sut;

    public ChangePasswordCommandHandlerTests()
    {
        sut = new ChangePasswordCommandHandler(
            users,
            hasher,
            clock,
            uow,
            sessions,
            PasswordGuards.Create(hasher, uow, clock),
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new AccountEmailComposer(new ApplicationOptions(), clock),
                NullLogger<AccountSecurityNotifier>.Instance
            )
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordNotSetReturnsBadRequest()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = (string?)null });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserPasswordNotSet);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncNewPasswordSameAsCurrentReturnsBadRequestWithoutCountingAnAttempt()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        var request = new ChangePasswordRequest("correct", "correct");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserNewPasswordSameAsCurrent);
        user.PasswordFailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
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
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordHash.Should().Be(hasher.Hash("correct"));
        user.PasswordFailedAttempts.Should().Be(1, "the guard counts the failure on its own");
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordRehashesAndPersists()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertSessionsRevokedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordNotifiesTheOwnerWithoutSecrets()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(user.Id, new ChangePasswordRequest("correct", "brandnew")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message
            .TextBody.Should()
            .NotContain("brandnew")
            .And.NotContain(user.PasswordHash)
            .And.NotContain("#userId=");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncNotificationFailureStillChangesThePassword()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(user.Id, new ChangePasswordRequest("correct", "brandnew")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertSessionsRevokedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncSaveChangesFailureDoesNotQueueTheAlert()
    {
        var user = NewUser();
        Persisted.Overwrite(user, new { PasswordHash = hasher.Hash("correct") });
        users.FindReturns(user);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("db down"));

        Func<Task> act = () =>
            sut.HandleAsync(
                new ChangePasswordCommand(
                    user.Id,
                    new ChangePasswordRequest("correct", "brandnew")
                ),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        emailSender.Sent.Should().BeEmpty("the alert is only queued once the commit succeeds");
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
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetExpiresAt.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
