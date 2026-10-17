using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly PasswordResetOptions passwordReset = new();
    private readonly AuditSubject auditSubject = new();
    private readonly ForgotPasswordCommandHandler sut;

    public ForgotPasswordCommandHandlerTests()
    {
        sut = new ForgotPasswordCommandHandler(
            users,
            AccountLocks.Create(users, Substitute.For<IUnitOfWork>()),
            clock,
            new FakeOneTimeCodeHasher(),
            passwordReset,
            new AccountEmails(
                emailSender,
                composer,
                verification,
                passwordReset,
                new TwoFactorOptions()
            ),
            NullLogger<ForgotPasswordCommandHandler>.Instance,
            auditSubject
        );
    }

    private Task<bool> AssertNeverLockedAsync()
    {
        return users
            .DidNotReceiveWithAnyArgs()
            .LockAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedStillReportsSuccessAndIssuesNoCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Global);
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand(user.Email!.Value),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCode.Hash.Should().BeNull();
        user.PasswordResetCode.LastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsSuccessWithoutSending()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("nobody@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        auditSubject.Entity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncUserWithoutPasswordReturnsSuccessWithoutSending()
    {
        users.FindReturns(NewUser(passwordHash: null));

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNeverLockedAsync();
    }

    public static TheoryData<Guid> IneligibleResetStatuses()
    {
        return [KnownIds.UserStatusTypes.Blocked, KnownIds.UserStatusTypes.Dependent];
    }

    [Theory]
    [MemberData(nameof(IneligibleResetStatuses))]
    public async Task HandleAsyncIneligibleStatusReturnsSuccessWithoutSending(Guid statusId)
    {
        users.FindReturns(NewUser(statusId: statusId));

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNeverLockedAsync();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsSuccessWithoutSending()
    {
        var user = users.FindReturns(NewUser());
        Persisted.Overwrite(
            user,
            new { PasswordResetCode = new { LastSentAt = clock.UtcNow.AddSeconds(-10) } }
        );

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNeverLockedAsync();
    }

    [Fact]
    public async Task HandleAsyncCodeSentByAParallelRequestBeforeTheLockSendsNothing()
    {
        var user = users.FindReturns(NewUser());
        users.ReloadsAs(
            user,
            new { PasswordResetCode = new { LastSentAt = clock.UtcNow.AddSeconds(-1) } }
        );

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAccountErasedBeforeTheLockReturnsSuccessWithoutSending()
    {
        var user = users.FindReturns(NewUser());
        users.LockAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEligibleUserSendsRandomCodeAndKeepsItsHash()
    {
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.PasswordReset));
        email.Recipient.Should().Be(new EmailRecipient(user.Email!.Value, user.FirstName));
        email.UserId.Should().Be(user.Id);
        auditSubject.Entity.Should().Be(new AuditEntity(AuditEntityType.User, user.Id.Value));
        email.Lifetime.Should().Be(passwordReset.CodeLifetime);
        var code = email.Code;
        code.Should()
            .MatchRegex("^[0-9a-f]{64}$", "the reset code is 256 random bits in lowercase hex");
        user.PasswordResetCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.PasswordResetCode.ExpiresAt.Should().Be(clock.UtcNow + passwordReset.CodeLifetime);
        user.PasswordResetCode.LastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncCooldownElapsedReplacesPreviousCode()
    {
        var user = users.FindReturns(
            NewUserWithResetCode(clock, code: "old-code", lastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var newCode = composer.Composed.Should().ContainSingle().Which.Code;
        newCode.Should().NotBe("old-code");
        user.PasswordResetCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + newCode);
    }

    [Fact]
    public async Task HandleAsyncEmailSendFailsReturnsSuccessWithoutIssuingCode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ForgotPasswordCommand("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCode.Hash.Should().BeNull();
    }
}
