using System.Linq.Expressions;
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
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<ChangePasswordCommandHandler>.Instance
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<int> AssertSessionsRevokedAsync()
    {
        return sessions
            .Received(1)
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<CancellationToken>()
            );
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
        user.PasswordHash = null;
        users.FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserPasswordNotSet);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncIncorrectCurrentPasswordReturnsBadRequest()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        users.FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordRehashesAndPersists()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
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
        await AssertSessionsRevokedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordNotifiesTheOwnerWithoutSecrets()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
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
        user.PasswordHash = hasher.Hash("correct");
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(user.Id, new ChangePasswordRequest("correct", "brandnew")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertSessionsRevokedAsync();
    }

    [Fact]
    public async Task HandleAsyncSaveChangesFailureDoesNotQueueTheAlert()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        users.FindReturns(user);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("db down"));

        Func<Task> act = () =>
            sut.HandleAsync(
                new ChangePasswordCommand(user.Id, new ChangePasswordRequest("correct", "brandnew")),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        emailSender.Sent.Should().BeEmpty("the alert is only queued once the commit succeeds");
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordClearsPendingPasswordReset()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
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
