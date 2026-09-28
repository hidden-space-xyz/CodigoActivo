using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class RequestAccountDeletionCodeCommandHandlerTests
{
    private const string Password = "Str0ngPass!23";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly TwoFactorOptions options = new();
    private readonly PasswordLockoutOptions lockout = new();
    private readonly RequestAccountDeletionCodeCommandHandler sut;

    public RequestAccountDeletionCodeCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        var accountEmails = new AccountEmails(
            emailSender,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            new ApplicationOptions { BaseUrl = "https://app.test" },
            options
        );
        sut = new RequestAccountDeletionCodeCommandHandler(
            users,
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock, sessions, emailSender, lockout),
            options,
            new LoginCodeIssuer(
                hasher,
                options,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private User Signed(
        bool isAdmin = false,
        string? passwordHash = FakePasswordHasher.Prefix + Password,
        string? email = "ana@test.com"
    )
    {
        var user = NewUser(isAdmin: isAdmin, email: email, passwordHash: passwordHash);
        users.Finds(user);
        return user;
    }

    private Task<Result> RequestAsync(Guid userId, string password = Password)
    {
        return sut.HandleAsync(
            new RequestAccountDeletionCodeCommand(userId, new AccountDeletionCodeRequest(password)),
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
        users.Finds(null);

        var result = await RequestAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var result = await RequestAsync(SeedIds.Users.InitialAdministrator, "WrongPassword!");

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .FindAsync(default!, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorEmailsTheCode()
    {
        var user = Signed(isAdmin: true);

        var result = await RequestAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutEmailing()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id, password: "WrongPassword!");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordFailedAttempts.Should().Be(1);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongPasswordsLockTheAccountRevokeSessionsAndWarnTheOwner()
    {
        var user = Signed();

        for (var attempt = 1; attempt <= lockout.MaxFailedAttempts; attempt++)
        {
            var result = await RequestAsync(user.Id, password: "WrongPassword!");

            result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
            user.PasswordFailedAttempts.Should().Be(attempt);
            user.IsPasswordLocked().Should().Be(attempt == lockout.MaxFailedAttempts);
        }

        user.PasswordLockedAt.Should().Be(clock.UtcNow);
        user.LoginCodeHash.Should().BeNull();
        await sessions
            .Received(1)
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<CancellationToken>()
            );
        var alert = emailSender.Sent.Should().ContainSingle().Subject;
        alert.Kind.Should().Be(EmailKind.SecurityAlert);
        alert.ToAddress.Should().Be("ana@test.com");
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountRefusesTheCorrectPasswordWithoutEmailing()
    {
        var user = Signed();
        user.PasswordLockedAt = clock.UtcNow.AddMinutes(-5);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.LoginCodeHash.Should().BeNull();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsBadRequest()
    {
        var user = Signed(passwordHash: null);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflictBecauseNoCodeIsEmailed()
    {
        var user = Signed();
        user.TwoFactorMethod = TwoFactorMethod.Authenticator;

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = Signed();
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = Signed();
        user.LoginCodeLastSentAt = clock.UtcNow.AddSeconds(-30);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutEmailPropagatesTheIssuerFailure()
    {
        var user = Signed(email: null);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictWithoutStagingACode()
    {
        var user = Signed();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await RequestAsync(user.Id);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordCorrectEmailsTheDeletionCodeAndStoresOnlyItsHash()
    {
        var user = Signed();
        user.LoginCodeLastSentAt = clock.UtcNow.AddMinutes(-5);

        var result = await RequestAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.Subject.Should().Contain("eliminación");
        var code = emailSender.LastLoginCode();
        code.Should().MatchRegex("^[0-9]{6}$");
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow + options.ChallengeLifetime);
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncNoPreviousCodeEmailsOne()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }
}
