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
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using TwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

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
    private readonly TestCurrentUser currentUser = new();
    private readonly RequestAccountDeletionCodeCommandHandler sut;

    public RequestAccountDeletionCodeCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        var accountEmails = new AccountEmails(
            emailSender,
            new AccountEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test" },
                new TestClock()
            ),
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        sut = new RequestAccountDeletionCodeCommandHandler(
            users,
            currentUser,
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
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new AccountDeletionCodeRequest(password).ToCommand(),
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

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var result = await RequestAsync(KnownIds.Users.InitialAdministrator, "WrongPassword!");

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorEmailsTheCode()
    {
        var user = Signed(isAdmin: true);

        var result = await RequestAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutEmailing()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id.Value, password: "WrongPassword!");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
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
            var result = await RequestAsync(user.Id.Value, password: "WrongPassword!");

            result.ShouldFail(
                ErrorKind.Validation,
                ApplicationErrorCode.UserCurrentPasswordIncorrect
            );
            user.PasswordFailedAttempts.Should().Be(attempt);
            user.IsPasswordLocked().Should().Be(attempt == lockout.MaxFailedAttempts);
        }

        user.PasswordLockedAt.Should().Be(clock.UtcNow);
        user.LoginCodeHash.Should().BeNull();
        await sessions.Received(1).EndAllAsync(user.Id, Arg.Any<CancellationToken>());
        var alert = emailSender.Sent.Should().ContainSingle().Subject;
        alert.Kind.Should().Be(EmailKind.SecurityAlert);
        alert.ToAddress.Should().Be("ana@test.com");
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountRefusesTheCorrectPasswordWithoutEmailing()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { PasswordLockedAt = clock.UtcNow.AddMinutes(-5) });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.LoginCodeHash.Should().BeNull();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsBadRequest()
    {
        var user = Signed(passwordHash: null);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflictBecauseNoCodeIsEmailed()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorMethod = TwoFactorMethod.Authenticator });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1) });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { LoginCodeLastSentAt = clock.UtcNow.AddSeconds(-30) });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutEmailPropagatesTheIssuerFailure()
    {
        var user = Signed(email: null);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictWithoutStagingACode()
    {
        var user = Signed();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordCorrectEmailsTheDeletionCodeAndStoresOnlyItsHash()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { LoginCodeLastSentAt = clock.UtcNow.AddMinutes(-5) });

        var result = await RequestAsync(user.Id.Value);

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
    }

    [Fact]
    public async Task HandleAsyncNoPreviousCodeEmailsOne()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCodeHash.Should().NotBeNull();
    }
}
